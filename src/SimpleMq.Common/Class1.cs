using System;

namespace SimpleMq.Common
{
    /// <summary>
    /// Represents the status of a message in the queue
    /// </summary>
    public enum MessageStatus
    {
        New = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>
    /// Represents a message in the queue
    /// </summary>
    public class QueueMessage
    {
        public long Id { get; set; }
        public MessageStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string RoutingKey { get; set; }
        public string Metadata { get; set; }
        public string Content { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Interface for sending messages to the queue
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a message to the queue
        /// </summary>
        /// <param name="routingKey">The routing key to determine the handler</param>
        /// <param name="content">The message content</param>
        /// <returns>The ID of the queued message</returns>
        long Send(string routingKey, object content);

        /// <summary>
        /// Sends a message to the queue with metadata
        /// </summary>
        /// <param name="routingKey">The routing key to determine the handler</param>
        /// <param name="content">The message content</param>
        /// <param name="metadata">Additional metadata for the message</param>
        /// <returns>The ID of the queued message</returns>
        long Send(string routingKey, object content, object metadata);
    }

    /// <summary>
    /// Interface for receiving messages from the queue
    /// </summary>
    public interface IReceiver
    {
        /// <summary>
        /// Receives the next available message from the queue
        /// </summary>
        /// <returns>The next message or null if no messages available</returns>
        QueueMessage Receive();

        /// <summary>
        /// Receives the next available message for a specific routing key
        /// </summary>
        /// <param name="routingKey">The routing key to filter by</param>
        /// <returns>The next message or null if no messages available</returns>
        QueueMessage Receive(string routingKey);
    }

    /// <summary>
    /// Interface for queue management utilities
    /// </summary>
    public interface IQueueManager
    {
        /// <summary>
        /// Gets a list of messages in the queue
        /// </summary>
        /// <param name="routingKey">Optional routing key filter</param>
        /// <param name="status">Optional status filter</param>
        /// <returns>List of queue messages</returns>
        QueueMessage[] GetMessages(string routingKey = null, MessageStatus? status = null);

        /// <summary>
        /// Updates the status of a message
        /// </summary>
        /// <param name="messageId">The ID of the message to update</param>
        /// <param name="status">The new status</param>
        /// <param name="error">Optional error message if status is Failed</param>
        void UpdateMessageStatus(long messageId, MessageStatus status, string error = null);

        /// <summary>
        /// Updates the status of multiple messages
        /// </summary>
        /// <param name="messageIds">The IDs of the messages to update</param>
        /// <param name="status">The new status</param>
        /// <param name="error">Optional error message if status is Failed</param>
        void UpdateMessageStatus(long[] messageIds, MessageStatus status, string error = null);
    }

    /// <summary>
    /// Simple sender implementation that uses the default connection
    /// </summary>
    public class Sender : ISender
    {
        private readonly ISender _implementation;

        public Sender() : this(Configuration.DefaultSender)
        {
        }

        public Sender(ISender implementation)
        {
            _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
        }

        public long Send(string routingKey, object content)
        {
            return _implementation.Send(routingKey, content);
        }

        public long Send(string routingKey, object content, object metadata)
        {
            return _implementation.Send(routingKey, content, metadata);
        }
    }

    /// <summary>
    /// Simple receiver implementation that uses the default connection
    /// </summary>
    public class Receiver : IReceiver
    {
        private readonly IReceiver _implementation;

        public Receiver() : this(Configuration.DefaultReceiver)
        {
        }

        public Receiver(IReceiver implementation)
        {
            _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
        }

        public QueueMessage Receive()
        {
            return _implementation.Receive();
        }

        public QueueMessage Receive(string routingKey)
        {
            return _implementation.Receive(routingKey);
        }
    }

    /// <summary>
    /// Configuration class for default implementations
    /// </summary>
    public static class Configuration
    {
        public static ISender DefaultSender { get; set; }
        public static IReceiver DefaultReceiver { get; set; }
        public static IQueueManager DefaultQueueManager { get; set; }
    }
}
