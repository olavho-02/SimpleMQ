using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleMq.SqlServer;
using System;

namespace SimpleMq.SqlServer.Test
{
    [TestClass]
    public class SqlServerSenderTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerSender_NullConnectionString_ThrowsException()
        {
            // Act
            new SqlServerSender(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerSender_NullTableName_ThrowsException()
        {
            // Act
            new SqlServerSender("connectionString", null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SqlServerSender_Send_NullRoutingKey_ThrowsException()
        {
            // Arrange
            var sender = new SqlServerSender("Server=test;Database=test;", "TestTable");

            // Act
            sender.Send(null, "content");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SqlServerSender_Send_EmptyRoutingKey_ThrowsException()
        {
            // Arrange
            var sender = new SqlServerSender("Server=test;Database=test;", "TestTable");

            // Act
            sender.Send("", "content");
        }
    }

    [TestClass]
    public class SqlServerReceiverTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerReceiver_NullConnectionString_ThrowsException()
        {
            // Act
            new SqlServerReceiver(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerReceiver_NullTableName_ThrowsException()
        {
            // Act
            new SqlServerReceiver("connectionString", null);
        }
    }

    [TestClass]
    public class SqlServerQueueManagerTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerQueueManager_NullConnectionString_ThrowsException()
        {
            // Act
            new SqlServerQueueManager(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SqlServerQueueManager_NullTableName_ThrowsException()
        {
            // Act
            new SqlServerQueueManager("connectionString", null);
        }
    }

    [TestClass]
    public class DatabaseInitializerTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DatabaseInitializer_InitializeDatabase_NullConnectionString_ThrowsException()
        {
            // Act
            DatabaseInitializer.InitializeDatabase(null);
        }
    }
}