# SimpleMQ Usage Examples

This document provides practical examples of how to use SimpleMQ in real-world scenarios.

## Quick Start

### 1. Database Setup

First, ensure your SQL Server database is ready and create the SimpleMQ table:

```csharp
using SimpleMq.SqlServer;

string connectionString = "Server=localhost;Database=MyApp;Trusted_Connection=true;";

// This creates the table if it doesn't exist
DatabaseInitializer.InitializeDatabase(connectionString);
```

### 2. Application Configuration

Configure SimpleMQ in your application startup (e.g., Global.asax.cs, Startup.cs, or Main()):

```csharp
using SimpleMq.Common;
using SimpleMq.SqlServer;

public class Startup
{
    public void ConfigureSimpleMQ()
    {
        string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        
        // Set up default implementations
        Configuration.DefaultSender = new SqlServerSender(connectionString);
        Configuration.DefaultReceiver = new SqlServerReceiver(connectionString);
        Configuration.DefaultQueueManager = new SqlServerQueueManager(connectionString);
    }
}
```

### 3. App.config / Web.config Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <connectionStrings>
    <add name="DefaultConnection" 
         connectionString="Server=localhost;Database=MyApplication;Trusted_Connection=true;TrustServerCertificate=true;" 
         providerName="Microsoft.Data.SqlClient" />
  </connectionStrings>
  
  <appSettings>
    <add key="SimpleMQ.TableName" value="SimpleMqMessages" />
    <add key="SimpleMQ.ProcessingInterval" value="5000" /> <!-- 5 seconds -->
  </appSettings>
</configuration>
```

## Sending Messages

### Simple Message Sending

```csharp
// As specified in requirements - simple usage
var result = (new SimpleMq.Sender()).Send("email.notification", new {
    To = "user@example.com",
    Subject = "Welcome!",
    Body = "Thank you for signing up."
});

Console.WriteLine($"Message queued with ID: {result}");
```

### Sending with Metadata

```csharp
var sender = new SimpleMq.Sender();

// Order processing with priority metadata
long messageId = sender.Send(
    "order.process",
    new { 
        OrderId = 12345, 
        CustomerId = 678,
        Items = new[] { 
            new { ProductId = 1, Quantity = 2 },
            new { ProductId = 5, Quantity = 1 }
        }
    },
    new { 
        Priority = "High", 
        Source = "WebAPI",
        RequestId = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow
    }
);
```

### Bulk Message Sending

```csharp
var sender = new SimpleMq.Sender();

// Send multiple notifications
var users = GetUsersToNotify();
foreach (var user in users)
{
    sender.Send("user.notification", new {
        UserId = user.Id,
        Type = "SystemUpdate",
        Message = "System maintenance scheduled for tonight."
    });
}
```

## Receiving Messages

### Simple Message Processing

```csharp
// As specified in requirements - simple usage
var msgResult = (new SimpleMq.Receiver()).Receive();

if (msgResult != null)
{
    Console.WriteLine($"Processing message {msgResult.Id} with routing key: {msgResult.RoutingKey}");
    
    // Decode the content
    string contentJson = System.Text.Encoding.UTF8.GetString(
        Convert.FromBase64String(msgResult.Content));
    
    // Process based on routing key
    ProcessMessage(msgResult.RoutingKey, contentJson);
}
```

### Routing-Specific Processing

```csharp
var receiver = new SimpleMq.Receiver();

// Only receive email notifications
var emailMessage = receiver.Receive("email.notification");
if (emailMessage != null)
{
    ProcessEmailNotification(emailMessage);
}

// Only receive order processing messages
var orderMessage = receiver.Receive("order.process");
if (orderMessage != null)
{
    ProcessOrder(orderMessage);
}
```

### Message Processing with Error Handling

```csharp
public void ProcessMessage(QueueMessage message)
{
    var queueManager = new SqlServerQueueManager(connectionString);
    
    try
    {
        // Decode content
        string contentJson = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(message.Content));
        
        // Route to handler
        var handler = GetHandlerForRoutingKey(message.RoutingKey);
        handler.Process(contentJson);
        
        // Mark as completed
        queueManager.UpdateMessageStatus(message.Id, MessageStatus.Completed);
        
        Console.WriteLine($"Successfully processed message {message.Id}");
    }
    catch (Exception ex)
    {
        // Mark as failed with error details
        queueManager.UpdateMessageStatus(message.Id, MessageStatus.Failed, ex.ToString());
        
        Console.WriteLine($"Failed to process message {message.Id}: {ex.Message}");
    }
}
```

## Queue Management

### Monitoring Queue Status

```csharp
var queueManager = new SqlServerQueueManager(connectionString);

// Get queue statistics
var newMessages = queueManager.GetMessages(status: MessageStatus.New);
var inProgressMessages = queueManager.GetMessages(status: MessageStatus.InProgress);
var failedMessages = queueManager.GetMessages(status: MessageStatus.Failed);

Console.WriteLine($"Queue Status:");
Console.WriteLine($"  New: {newMessages.Length}");
Console.WriteLine($"  In Progress: {inProgressMessages.Length}");
Console.WriteLine($"  Failed: {failedMessages.Length}");
```

### Filtering Messages

```csharp
var queueManager = new SqlServerQueueManager(connectionString);

// Get all failed email notifications
var failedEmails = queueManager.GetMessages("email.notification", MessageStatus.Failed);

// Get all order processing messages (any status)
var orderMessages = queueManager.GetMessages("order.process");

// Get all new messages (any routing key)
var allNewMessages = queueManager.GetMessages(status: MessageStatus.New);
```

### Retry Failed Messages

```csharp
var queueManager = new SqlServerQueueManager(connectionString);

// Get failed messages that are older than 1 hour
var failedMessages = queueManager.GetMessages(status: MessageStatus.Failed)
    .Where(m => m.CompletedDate.HasValue && 
                m.CompletedDate.Value < DateTime.UtcNow.AddHours(-1))
    .Take(10) // Retry only 10 at a time
    .ToArray();

if (failedMessages.Any())
{
    var failedIds = failedMessages.Select(m => m.Id).ToArray();
    
    // Reset to New status for retry
    queueManager.UpdateMessageStatus(failedIds, MessageStatus.New);
    
    Console.WriteLine($"Reset {failedIds.Length} failed messages for retry");
}
```

## Windows Service Example

### Service Implementation

```csharp
using System;
using System.Configuration;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using SimpleMq.Common;
using SimpleMq.SqlServer;

public partial class SimpleMqProcessorService : ServiceBase
{
    private CancellationTokenSource _cancellationTokenSource;
    private Task _processingTask;
    private IReceiver _receiver;
    private IQueueManager _queueManager;

    public SimpleMqProcessorService()
    {
        InitializeComponent();
        ServiceName = "SimpleMQ Processor";
    }

    protected override void OnStart(string[] args)
    {
        string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        _receiver = new SqlServerReceiver(connectionString);
        _queueManager = new SqlServerQueueManager(connectionString);
        
        _cancellationTokenSource = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessMessagesAsync(_cancellationTokenSource.Token));
        
        WriteToEventLog("SimpleMQ Processor Service started");
    }

    protected override void OnStop()
    {
        _cancellationTokenSource?.Cancel();
        _processingTask?.Wait(TimeSpan.FromSeconds(30));
        
        WriteToEventLog("SimpleMQ Processor Service stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        int intervalMs = int.Parse(ConfigurationManager.AppSettings["SimpleMQ.ProcessingInterval"] ?? "5000");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ProcessAvailableMessages();
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                WriteToEventLog($"Error in message processing loop: {ex}", EventLogEntryType.Error);
                await Task.Delay(intervalMs, cancellationToken);
            }
        }
    }

    private void ProcessAvailableMessages()
    {
        QueueMessage message;
        int processedCount = 0;
        
        // Process up to 10 messages per cycle to avoid blocking
        while ((message = _receiver.Receive()) != null && processedCount < 10)
        {
            ProcessSingleMessage(message);
            processedCount++;
        }
        
        if (processedCount > 0)
        {
            WriteToEventLog($"Processed {processedCount} messages");
        }
    }

    private void ProcessSingleMessage(QueueMessage message)
    {
        try
        {
            var handler = MessageHandlerFactory.GetHandler(message.RoutingKey);
            
            if (handler != null)
            {
                handler.ProcessMessage(message);
                _queueManager.UpdateMessageStatus(message.Id, MessageStatus.Completed);
            }
            else
            {
                _queueManager.UpdateMessageStatus(message.Id, MessageStatus.Failed, 
                    $"No handler found for routing key: {message.RoutingKey}");
            }
        }
        catch (Exception ex)
        {
            _queueManager.UpdateMessageStatus(message.Id, MessageStatus.Failed, ex.ToString());
            WriteToEventLog($"Failed to process message {message.Id}: {ex.Message}", EventLogEntryType.Warning);
        }
    }

    private void WriteToEventLog(string message, EventLogEntryType entryType = EventLogEntryType.Information)
    {
        try
        {
            if (!EventLog.SourceExists(ServiceName))
            {
                EventLog.CreateEventSource(ServiceName, "Application");
            }
            
            EventLog.WriteEntry(ServiceName, message, entryType);
        }
        catch
        {
            // Ignore event log errors
        }
    }
}
```

### Message Handler Factory

```csharp
public static class MessageHandlerFactory
{
    private static readonly Dictionary<string, Func<IMessageHandler>> _handlers = 
        new Dictionary<string, Func<IMessageHandler>>
        {
            { "email.notification", () => new EmailNotificationHandler() },
            { "order.process", () => new OrderProcessingHandler() },
            { "user.notification", () => new UserNotificationHandler() },
            { "report.generate", () => new ReportGenerationHandler() }
        };

    public static IMessageHandler GetHandler(string routingKey)
    {
        if (_handlers.TryGetValue(routingKey, out var factory))
        {
            return factory();
        }
        
        return null;
    }
}

public interface IMessageHandler
{
    void ProcessMessage(QueueMessage message);
}
```

### Example Message Handler

```csharp
public class EmailNotificationHandler : IMessageHandler
{
    public void ProcessMessage(QueueMessage message)
    {
        // Decode the content
        string contentJson = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(message.Content));
        
        dynamic emailData = Newtonsoft.Json.JsonConvert.DeserializeObject(contentJson);
        
        // Send email using your preferred email service
        var emailService = new EmailService();
        emailService.SendEmail(
            to: emailData.To,
            subject: emailData.Subject,
            body: emailData.Body
        );
        
        Console.WriteLine($"Email sent to {emailData.To}: {emailData.Subject}");
    }
}
```

## Testing and Debugging

### Unit Testing Message Processing

```csharp
[TestMethod]
public void TestMessageProcessing()
{
    // Arrange
    string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=SimpleMQTest;Integrated Security=true;";
    DatabaseInitializer.InitializeDatabase(connectionString);
    
    var sender = new SqlServerSender(connectionString);
    var receiver = new SqlServerReceiver(connectionString);
    var queueManager = new SqlServerQueueManager(connectionString);
    
    // Act
    long messageId = sender.Send("test.message", new { Data = "Test content" });
    var receivedMessage = receiver.Receive();
    
    // Assert
    Assert.IsNotNull(receivedMessage);
    Assert.AreEqual(messageId, receivedMessage.Id);
    Assert.AreEqual("test.message", receivedMessage.RoutingKey);
    Assert.AreEqual(MessageStatus.InProgress, receivedMessage.Status);
    
    // Cleanup
    queueManager.UpdateMessageStatus(messageId, MessageStatus.Completed);
}
```

### Performance Testing

```csharp
public void PerformanceTest()
{
    var sender = new SqlServerSender(connectionString);
    var stopwatch = Stopwatch.StartNew();
    
    // Send 1000 messages
    for (int i = 0; i < 1000; i++)
    {
        sender.Send("perf.test", new { MessageNumber = i, Timestamp = DateTime.UtcNow });
    }
    
    stopwatch.Stop();
    Console.WriteLine($"Sent 1000 messages in {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"Average: {stopwatch.ElapsedMilliseconds / 1000.0}ms per message");
}
```

This covers the most common usage patterns for SimpleMQ. The implementation provides a simple but powerful foundation for reliable message queuing in .NET Framework 4.7.2 applications.