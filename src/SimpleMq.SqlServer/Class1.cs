using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SimpleMq.Common;
using System.Text;
using Newtonsoft.Json;

namespace SimpleMq.SqlServer
{
    /// <summary>
    /// SQL Server implementation of ISender
    /// </summary>
    public class SqlServerSender : ISender
    {
        private readonly string _connectionString;
        private readonly string _tableName;

        public SqlServerSender(string connectionString, string tableName = "SimpleMqMessages")
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        public long Send(string routingKey, object content)
        {
            return Send(routingKey, content, null);
        }

        public long Send(string routingKey, object content, object metadata)
        {
            if (string.IsNullOrEmpty(routingKey))
                throw new ArgumentException("Routing key cannot be null or empty", nameof(routingKey));

            string contentJson = content != null ? JsonConvert.SerializeObject(content) : null;
            string metadataJson = metadata != null ? JsonConvert.SerializeObject(metadata) : null;

            string contentBase64 = contentJson != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(contentJson)) : null;
            string metadataBase64 = metadataJson != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(metadataJson)) : null;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                string sql = $@"
                    INSERT INTO [{_tableName}] (Status, CreatedDate, RoutingKey, Metadata, Content)
                    OUTPUT INSERTED.Id
                    VALUES (@Status, @CreatedDate, @RoutingKey, @Metadata, @Content)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Status", (int)MessageStatus.New);
                    command.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@RoutingKey", routingKey);
                    command.Parameters.AddWithValue("@Metadata", (object)metadataBase64 ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Content", (object)contentBase64 ?? DBNull.Value);

                    return (long)command.ExecuteScalar();
                }
            }
        }
    }

    /// <summary>
    /// SQL Server implementation of IReceiver
    /// </summary>
    public class SqlServerReceiver : IReceiver
    {
        private readonly string _connectionString;
        private readonly string _tableName;

        public SqlServerReceiver(string connectionString, string tableName = "SimpleMqMessages")
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        public QueueMessage Receive()
        {
            return Receive(null);
        }

        public QueueMessage Receive(string routingKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Find and claim a message
                        string whereClause = "WHERE Status = @Status";
                        if (!string.IsNullOrEmpty(routingKey))
                        {
                            whereClause += " AND RoutingKey = @RoutingKey";
                        }

                        string selectSql = $@"
                            SELECT TOP 1 Id, Status, CreatedDate, CompletedDate, RoutingKey, Metadata, Content, Error
                            FROM [{_tableName}] WITH (UPDLOCK, READPAST)
                            {whereClause}
                            ORDER BY Id";

                        QueueMessage message = null;

                        using (var selectCommand = new SqlCommand(selectSql, connection, transaction))
                        {
                            selectCommand.Parameters.AddWithValue("@Status", (int)MessageStatus.New);
                            if (!string.IsNullOrEmpty(routingKey))
                            {
                                selectCommand.Parameters.AddWithValue("@RoutingKey", routingKey);
                            }

                            using (var reader = selectCommand.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    message = MapFromReader(reader);
                                }
                            }
                        }

                        if (message != null)
                        {
                            // Update status to InProgress
                            string updateSql = $"UPDATE [{_tableName}] SET Status = @NewStatus WHERE Id = @Id";
                            
                            using (var updateCommand = new SqlCommand(updateSql, connection, transaction))
                            {
                                updateCommand.Parameters.AddWithValue("@NewStatus", (int)MessageStatus.InProgress);
                                updateCommand.Parameters.AddWithValue("@Id", message.Id);
                                updateCommand.ExecuteNonQuery();
                            }

                            message.Status = MessageStatus.InProgress;
                        }

                        transaction.Commit();
                        return message;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private QueueMessage MapFromReader(SqlDataReader reader)
        {
            return new QueueMessage
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Status = (MessageStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                RoutingKey = reader.GetString(reader.GetOrdinal("RoutingKey")),
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata")),
                Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? null : reader.GetString(reader.GetOrdinal("Content")),
                Error = reader.IsDBNull(reader.GetOrdinal("Error")) ? null : reader.GetString(reader.GetOrdinal("Error"))
            };
        }
    }

    /// <summary>
    /// SQL Server implementation of IQueueManager
    /// </summary>
    public class SqlServerQueueManager : IQueueManager
    {
        private readonly string _connectionString;
        private readonly string _tableName;

        public SqlServerQueueManager(string connectionString, string tableName = "SimpleMqMessages")
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        public QueueMessage[] GetMessages(string routingKey = null, MessageStatus? status = null)
        {
            var messages = new List<QueueMessage>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var whereConditions = new List<string>();
                if (!string.IsNullOrEmpty(routingKey))
                    whereConditions.Add("RoutingKey = @RoutingKey");
                if (status.HasValue)
                    whereConditions.Add("Status = @Status");

                string whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";

                string sql = $@"
                    SELECT Id, Status, CreatedDate, CompletedDate, RoutingKey, Metadata, Content, Error
                    FROM [{_tableName}]
                    {whereClause}
                    ORDER BY Id";

                using (var command = new SqlCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(routingKey))
                        command.Parameters.AddWithValue("@RoutingKey", routingKey);
                    if (status.HasValue)
                        command.Parameters.AddWithValue("@Status", (int)status.Value);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            messages.Add(MapFromReader(reader));
                        }
                    }
                }
            }

            return messages.ToArray();
        }

        public void UpdateMessageStatus(long messageId, MessageStatus status, string error = null)
        {
            UpdateMessageStatus(new[] { messageId }, status, error);
        }

        public void UpdateMessageStatus(long[] messageIds, MessageStatus status, string error = null)
        {
            if (messageIds == null || messageIds.Length == 0)
                return;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var idParams = new List<string>();
                for (int i = 0; i < messageIds.Length; i++)
                {
                    idParams.Add($"@Id{i}");
                }

                string sql = $@"
                    UPDATE [{_tableName}] 
                    SET Status = @Status, 
                        CompletedDate = @CompletedDate,
                        Error = @Error
                    WHERE Id IN ({string.Join(",", idParams)})";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Status", (int)status);
                    command.Parameters.AddWithValue("@CompletedDate", 
                        status == MessageStatus.Completed || status == MessageStatus.Failed 
                            ? (object)DateTime.UtcNow 
                            : DBNull.Value);
                    command.Parameters.AddWithValue("@Error", (object)error ?? DBNull.Value);

                    for (int i = 0; i < messageIds.Length; i++)
                    {
                        command.Parameters.AddWithValue($"@Id{i}", messageIds[i]);
                    }

                    command.ExecuteNonQuery();
                }
            }
        }

        private QueueMessage MapFromReader(SqlDataReader reader)
        {
            return new QueueMessage
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Status = (MessageStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                RoutingKey = reader.GetString(reader.GetOrdinal("RoutingKey")),
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata")),
                Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? null : reader.GetString(reader.GetOrdinal("Content")),
                Error = reader.IsDBNull(reader.GetOrdinal("Error")) ? null : reader.GetString(reader.GetOrdinal("Error"))
            };
        }
    }

    /// <summary>
    /// Utility class for creating and managing the SQL Server database schema
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Creates the message table if it doesn't exist
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string</param>
        /// <param name="tableName">The name of the table to create</param>
        public static void InitializeDatabase(string connectionString, string tableName = "SimpleMqMessages")
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sql = $@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{tableName}' AND xtype='U')
                    BEGIN
                        CREATE TABLE [{tableName}] (
                            [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
                            [Status] INT NOT NULL DEFAULT 0,
                            [CreatedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            [CompletedDate] DATETIME2 NULL,
                            [RoutingKey] NVARCHAR(255) NOT NULL,
                            [Metadata] NVARCHAR(MAX) NULL,
                            [Content] NVARCHAR(MAX) NULL,
                            [Error] NVARCHAR(MAX) NULL
                        );

                        CREATE INDEX IX_{tableName}_Status_RoutingKey ON [{tableName}] (Status, RoutingKey);
                        CREATE INDEX IX_{tableName}_CreatedDate ON [{tableName}] (CreatedDate);
                    END";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
