# SimpleMQ

SimpleMQ is a simple, database-supported queue implementation for .NET applications using SQL Server as the backing store. It provides a lightweight alternative to more complex message queue systems when you need reliable message queuing with minimal setup.

## Features

- ✅ **Simple API**: Easy-to-use Send/Receive patterns
- ✅ **SQL Server Backend**: Uses familiar SQL Server for persistence and reliability
- ✅ **.NET Framework 4.7.2 Compatible**: Designed for legacy .NET Framework applications
- ✅ **Transactional Safety**: All operations are database-transaction safe
- ✅ **Message Status Tracking**: Track messages through New → InProgress → Completed/Failed states
- ✅ **Routing Keys**: Route messages to specific handlers using routing keys
- ✅ **Queue Management**: Built-in utilities for monitoring and managing queue state
- ✅ **Error Handling**: Failed messages are preserved with error details for analysis
- ✅ **Concurrency Safe**: Multiple processes can safely consume from the same queue

## Quick Start

### 1. Install Dependencies

Add references to:
- `SimpleMq.Common.dll` - Core interfaces and models
- `SimpleMq.SqlServer.dll` - SQL Server implementation

### 2. Database Setup

```csharp
using SimpleMq.SqlServer;

string connectionString = "Server=localhost;Database=MyApp;Trusted_Connection=true;";
DatabaseInitializer.InitializeDatabase(connectionString);
```

### 3. Configure SimpleMQ

```csharp
using SimpleMq.Common;
using SimpleMq.SqlServer;

// Set up default implementations
Configuration.DefaultSender = new SqlServerSender(connectionString);
Configuration.DefaultReceiver = new SqlServerReceiver(connectionString);
Configuration.DefaultQueueManager = new SqlServerQueueManager(connectionString);
```

### 4. Send Messages

```csharp
// Simple usage as per requirements
var result = (new SimpleMq.Sender()).Send("email.notification", new {
    To = "user@example.com",
    Subject = "Welcome!",
    Body = "Thank you for signing up."
});
```

### 5. Receive Messages

```csharp
// Simple usage as per requirements
var msgResult = (new SimpleMq.Receiver()).Receive();

if (msgResult != null)
{
    // Process the message
    ProcessMessage(msgResult);
    
    // Mark as completed
    var queueManager = new SqlServerQueueManager(connectionString);
    queueManager.UpdateMessageStatus(msgResult.Id, MessageStatus.Completed);
}
```

## Documentation

- **[Implementation Plan](docs/IMPLEMENTATION_PLAN.md)** - Complete technical implementation details and architecture
- **[Usage Examples](docs/USAGE_EXAMPLES.md)** - Practical examples and patterns for real-world usage

## Database Schema

SimpleMQ uses a single table to store all queue messages:

| Column | Type | Description |
|--------|------|-------------|
| Id | BIGINT IDENTITY | Auto-incrementing message ID |
| Status | INT | Message status (0=New, 1=InProgress, 2=Completed, 3=Failed) |
| CreatedDate | DATETIME2 | UTC timestamp when message was created |
| CompletedDate | DATETIME2 | UTC timestamp when message was completed/failed |
| RoutingKey | NVARCHAR(255) | Key used to route message to appropriate handler |
| Metadata | NVARCHAR(MAX) | Base64-encoded metadata (optional) |
| Content | NVARCHAR(MAX) | Base64-encoded JSON content |
| Error | NVARCHAR(MAX) | Error message if processing failed |

## API Reference

### Core Interfaces

#### Sender
```csharp
var sender = new SimpleMq.Sender();
long messageId = sender.Send(routingKey, content);
long messageId = sender.Send(routingKey, content, metadata);
```

#### Receiver
```csharp
var receiver = new SimpleMq.Receiver();
QueueMessage message = receiver.Receive();                    // Any routing key
QueueMessage message = receiver.Receive("specific.route");    // Specific routing key
```

#### Queue Manager
```csharp
var queueManager = new SqlServerQueueManager(connectionString);

// List messages with optional filtering
QueueMessage[] messages = queueManager.GetMessages();
QueueMessage[] messages = queueManager.GetMessages("email.notification");
QueueMessage[] messages = queueManager.GetMessages(status: MessageStatus.Failed);

// Update message status
queueManager.UpdateMessageStatus(messageId, MessageStatus.Completed);
queueManager.UpdateMessageStatus(messageIds, MessageStatus.Failed, "Error details");
```

## Deployment Scenarios

### Application Integration
Use SimpleMQ libraries in your .NET Framework 4.7.2 applications to send messages to queues.

### Windows Service
Create a Windows Service that processes messages from the queue:

```csharp
public class MessageProcessorService : ServiceBase
{
    private Timer _timer;
    private IReceiver _receiver;
    
    protected override void OnStart(string[] args)
    {
        _receiver = new SqlServerReceiver(connectionString);
        _timer = new Timer(ProcessMessages, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
    
    private void ProcessMessages(object state)
    {
        QueueMessage message;
        while ((message = _receiver.Receive()) != null)
        {
            // Route and process message based on routing key
            ProcessMessage(message);
        }
    }
}
```

## Requirements Met

This implementation addresses all specified requirements:

- ✅ **Framework Compatibility**: Targets .NET Framework 4.7.2
- ✅ **Database Table Structure**: 
  - Bigint auto-incremented ID
  - Status field (New, InProgress, Completed, Failed)
  - Created and completed date fields
  - Routing key for handler determination
  - Base64-encoded metadata and content
  - Error column for failure details
- ✅ **Simple Send API**: `(new SimpleMq.Sender()).Send(routingKey, content)`
- ✅ **Simple Receive API**: `(new SimpleMq.Receiver()).Receive()`
- ✅ **Queue Utilities**: Filtering by routing key and status
- ✅ **Status Management**: Update individual or multiple message statuses
- ✅ **Deployment Support**: Libraries for sending + Windows Service for processing

## Additional Features

Beyond the basic requirements, SimpleMQ includes:

- **Automatic Schema Creation**: Database table and indexes created automatically
- **Connection Management**: Proper SQL connection handling and disposal  
- **Transaction Safety**: All critical operations are transactional
- **Concurrent Processing**: Safe for multiple consumers using SQL Server locking
- **Base64 Encoding**: Content and metadata are safely encoded for storage
- **JSON Serialization**: Objects are automatically serialized to JSON
- **Comprehensive Testing**: Unit tests for all core functionality
- **Extensive Documentation**: Complete implementation plan and usage examples

## Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests (Note: requires Mono for .NET Framework tests in some environments)
dotnet test
```

## License

[MIT License](LICENSE) - See LICENSE file for details.

## Contributing

This project was implemented as a focused solution for specific requirements. For enhancements or issues, please refer to the implementation plan for architecture guidance.
