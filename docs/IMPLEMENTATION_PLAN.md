# SimpleMQ Implementation Plan

## Overview
SimpleMQ is a simple, database-supported queue implementation for .NET applications using SQL Server as the backing store. This document outlines the complete implementation plan and architecture decisions.

## Requirements Analysis

### Core Requirements
1. **Framework Compatibility**: Must be compatible with .NET Framework 4.7.2
2. **Database Backend**: SQL Server with a simple table structure
3. **Simple API**: Easy-to-use Send/Receive patterns
4. **Queue Management**: Utilities for listing and updating queue items
5. **Deployment Scenarios**: 
   - Library for sending messages from applications
   - Windows service for processing messages

### Database Schema
The queue is implemented as a single SQL Server table with the following structure:

```sql
CREATE TABLE [SimpleMqMessages] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [Status] INT NOT NULL DEFAULT 0,
    [CreatedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CompletedDate] DATETIME2 NULL,
    [RoutingKey] NVARCHAR(255) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [Content] NVARCHAR(MAX) NULL,
    [Error] NVARCHAR(MAX) NULL
);
```

**Status Values:**
- 0 = New
- 1 = InProgress
- 2 = Completed
- 3 = Failed

## Architecture

### Project Structure
```
SimpleMQ/
├── src/
│   ├── SimpleMq.Common/           # Core interfaces and abstractions
│   │   ├── QueueMessage.cs        # Message model
│   │   ├── MessageStatus.cs       # Status enumeration
│   │   ├── ISender.cs             # Sender interface
│   │   ├── IReceiver.cs           # Receiver interface
│   │   ├── IQueueManager.cs       # Queue management interface
│   │   ├── Sender.cs              # Simple sender wrapper
│   │   ├── Receiver.cs            # Simple receiver wrapper
│   │   └── Configuration.cs       # Default configuration
│   └── SimpleMq.SqlServer/        # SQL Server implementation
│       ├── SqlServerSender.cs     # SQL Server sender implementation
│       ├── SqlServerReceiver.cs   # SQL Server receiver implementation
│       ├── SqlServerQueueManager.cs # SQL Server queue manager
│       └── DatabaseInitializer.cs # Schema creation utility
└── test/                          # Unit tests
```

### Core Interfaces

#### ISender
```csharp
public interface ISender
{
    long Send(string routingKey, object content);
    long Send(string routingKey, object content, object metadata);
}
```

#### IReceiver
```csharp
public interface IReceiver
{
    QueueMessage Receive();
    QueueMessage Receive(string routingKey);
}
```

#### IQueueManager
```csharp
public interface IQueueManager
{
    QueueMessage[] GetMessages(string routingKey = null, MessageStatus? status = null);
    void UpdateMessageStatus(long messageId, MessageStatus status, string error = null);
    void UpdateMessageStatus(long[] messageIds, MessageStatus status, string error = null);
}
```

## Implementation Details

### Message Serialization
- **Content**: Objects are serialized to JSON using Newtonsoft.Json, then base64-encoded for storage
- **Metadata**: Optional metadata is similarly serialized and base64-encoded
- **Encoding**: UTF-8 encoding is used for all text data

### Concurrency Handling
- **Receive Operations**: Uses SQL Server's `UPDLOCK, READPAST` hints to prevent race conditions
- **Transaction Safety**: All critical operations are wrapped in database transactions
- **Atomic Updates**: Message status changes are atomic to prevent inconsistent states

### Error Handling
- **Connection Failures**: SQL exceptions are propagated to calling code
- **Validation**: Input validation with appropriate ArgumentException/ArgumentNullException
- **Transaction Rollback**: Failed operations are rolled back to maintain data integrity

## Usage Examples

### Basic Setup
```csharp
// Initialize the database (run once)
string connectionString = "Server=localhost;Database=SimpleMQ;Trusted_Connection=true;";
DatabaseInitializer.InitializeDatabase(connectionString);

// Configure default implementations
Configuration.DefaultSender = new SqlServerSender(connectionString);
Configuration.DefaultReceiver = new SqlServerReceiver(connectionString);
Configuration.DefaultQueueManager = new SqlServerQueueManager(connectionString);
```

### Sending Messages
```csharp
// Simple usage (as specified in requirements)
var result = (new SimpleMq.Sender()).Send("email.notification", new { 
    To = "user@example.com", 
    Subject = "Welcome", 
    Body = "Welcome to our service!" 
});

// With metadata
var sender = new SimpleMq.Sender();
long messageId = sender.Send("order.processing", 
    new { OrderId = 12345, CustomerId = 678 },
    new { Priority = "High", Source = "WebAPI" });
```

### Receiving Messages
```csharp
// Simple usage (as specified in requirements)
var msgResult = (new SimpleMq.Receiver()).Receive();

// Process the message
if (msgResult != null)
{
    try
    {
        // Decode content
        string contentJson = Encoding.UTF8.GetString(Convert.FromBase64String(msgResult.Content));
        var content = JsonConvert.DeserializeObject(contentJson);
        
        // Process the message based on routing key
        ProcessMessage(msgResult.RoutingKey, content);
        
        // Mark as completed
        var queueManager = new SqlServerQueueManager(connectionString);
        queueManager.UpdateMessageStatus(msgResult.Id, MessageStatus.Completed);
    }
    catch (Exception ex)
    {
        // Mark as failed with error
        var queueManager = new SqlServerQueueManager(connectionString);
        queueManager.UpdateMessageStatus(msgResult.Id, MessageStatus.Failed, ex.Message);
    }
}
```

### Queue Management
```csharp
var queueManager = new SqlServerQueueManager(connectionString);

// Get all pending messages
var pendingMessages = queueManager.GetMessages(status: MessageStatus.New);

// Get failed email notifications
var failedEmails = queueManager.GetMessages("email.notification", MessageStatus.Failed);

// Retry failed messages (reset to New status)
var failedIds = failedEmails.Select(m => m.Id).ToArray();
queueManager.UpdateMessageStatus(failedIds, MessageStatus.New);
```

## Windows Service Implementation Pattern

For the message processing Windows service:

```csharp
public class MessageProcessorService : ServiceBase
{
    private Timer _timer;
    private IReceiver _receiver;
    private IQueueManager _queueManager;
    
    protected override void OnStart(string[] args)
    {
        string connectionString = ConfigurationManager.ConnectionStrings["SimpleMQ"].ConnectionString;
        _receiver = new SqlServerReceiver(connectionString);
        _queueManager = new SqlServerQueueManager(connectionString);
        
        _timer = new Timer(ProcessMessages, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
    
    private void ProcessMessages(object state)
    {
        try
        {
            QueueMessage message;
            while ((message = _receiver.Receive()) != null)
            {
                try
                {
                    // Route to appropriate handler based on routing key
                    var handler = HandlerFactory.GetHandler(message.RoutingKey);
                    handler.Process(message);
                    
                    _queueManager.UpdateMessageStatus(message.Id, MessageStatus.Completed);
                }
                catch (Exception ex)
                {
                    _queueManager.UpdateMessageStatus(message.Id, MessageStatus.Failed, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            // Log service-level errors
            EventLog.WriteEntry("SimpleMQ Service", ex.ToString(), EventLogEntryType.Error);
        }
    }
}
```

## Additional Considerations

### Performance Optimizations
1. **Connection Pooling**: SQL Server connection pooling is enabled by default
2. **Batch Operations**: Queue manager supports batch status updates
3. **Indexing**: Appropriate indexes on Status, RoutingKey, and CreatedDate
4. **Message Cleanup**: Consider implementing automated cleanup of old completed messages

### Security Considerations
1. **SQL Injection**: All queries use parameterized commands
2. **Connection Security**: Support for encrypted connections to SQL Server
3. **Input Validation**: Validation of routing keys and content sizes

### Monitoring and Maintenance
1. **Dead Letter Queue**: Failed messages remain in the table for analysis
2. **Queue Metrics**: Query the table directly for queue depth and processing rates
3. **Message Retention**: Implement cleanup policies for old messages

### Future Enhancements
1. **Message Priorities**: Add priority field for message ordering
2. **Message Expiration**: Add TTL support for messages
3. **Retry Logic**: Built-in retry mechanisms with exponential backoff
4. **Multiple Queues**: Support for multiple named queues
5. **Poison Message Handling**: Automatic dead-lettering after max retries

## Deployment Checklist

### Application Deployment
- [ ] Install .NET Framework 4.7.2 runtime
- [ ] Deploy SimpleMq.Common.dll and SimpleMq.SqlServer.dll
- [ ] Configure connection string in app.config/web.config
- [ ] Initialize database schema using DatabaseInitializer
- [ ] Configure default implementations in application startup

### Windows Service Deployment
- [ ] Create Windows Service project referencing SimpleMQ libraries
- [ ] Implement message handlers for each routing key
- [ ] Configure service connection string
- [ ] Install and configure Windows Service
- [ ] Set up service monitoring and logging
- [ ] Test message processing end-to-end

This implementation provides a solid foundation for a simple, reliable message queue system that meets all the specified requirements while being extensible for future enhancements.