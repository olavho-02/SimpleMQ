using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleMq.Common;
using System;

namespace SimpleMq.Common.Test
{
    [TestClass]
    public class QueueMessageTests
    {
        [TestMethod]
        public void QueueMessage_DefaultConstructor_SetsDefaults()
        {
            // Arrange & Act
            var message = new QueueMessage();

            // Assert
            Assert.AreEqual(0L, message.Id);
            Assert.AreEqual(MessageStatus.New, message.Status);
            Assert.AreEqual(DateTime.MinValue, message.CreatedDate);
            Assert.IsNull(message.CompletedDate);
            Assert.IsNull(message.RoutingKey);
            Assert.IsNull(message.Metadata);
            Assert.IsNull(message.Content);
            Assert.IsNull(message.Error);
        }

        [TestMethod]
        public void MessageStatus_HasCorrectValues()
        {
            // Assert
            Assert.AreEqual(0, (int)MessageStatus.New);
            Assert.AreEqual(1, (int)MessageStatus.InProgress);
            Assert.AreEqual(2, (int)MessageStatus.Completed);
            Assert.AreEqual(3, (int)MessageStatus.Failed);
        }
    }

    [TestClass]
    public class SenderTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Sender_NullImplementation_ThrowsException()
        {
            // Act
            new Sender(null);
        }
    }

    [TestClass]
    public class ReceiverTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Receiver_NullImplementation_ThrowsException()
        {
            // Act
            new Receiver(null);
        }
    }
}