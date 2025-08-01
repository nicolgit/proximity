using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace api.Services
{
    /// <summary>
    /// Service for managing Azure Storage operations with secure connection handling
    /// </summary>
    public class StorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly TableServiceClient _tableServiceClient;
        private readonly ILogger<StorageService> _logger;
        private readonly string _connectionString;

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetValue<string>("CustomStorageConnectionString")
                ?? throw new InvalidOperationException("CustomStorageConnectionString is not configured");

            try
            {
                // Initialize BlobServiceClient with connection string or managed identity
                if (_connectionString.Contains("UseDevelopmentStorage=true") || _connectionString.Contains("AccountKey="))
                {
                    // Use connection string for local development or when key is provided
                    _blobServiceClient = new BlobServiceClient(_connectionString);
                    _tableServiceClient = new TableServiceClient(_connectionString);
                    _logger.LogInformation("Initialized BlobServiceClient and TableServiceClient with connection string");
                }
                else
                {
                    // Use managed identity for production scenarios
                    var credential = new DefaultAzureCredential();
                    _blobServiceClient = new BlobServiceClient(new Uri(_connectionString), credential);
                    _tableServiceClient = new TableServiceClient(new Uri(_connectionString), credential);
                    _logger.LogInformation("Initialized BlobServiceClient and TableServiceClient with managed identity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Storage clients");
                throw;
            }
        }

        /// <summary>
        /// Tests the connection to Azure Storage by attempting to get service properties
        /// </summary>
        /// <returns>True if connection is successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Azure Storage connection...");

                // Attempt to get service properties to test connection
                var properties = await _blobServiceClient.GetPropertiesAsync();

                _logger.LogInformation("Azure Storage connection test successful. Service version: {ServiceVersion}",
                    properties.Value.DefaultServiceVersion);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Storage connection test failed: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets a container client for the specified container name
        /// </summary>
        /// <param name="containerName">The name of the container</param>
        /// <returns>BlobContainerClient instance</returns>
        public BlobContainerClient GetContainerClient(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

            return _blobServiceClient.GetBlobContainerClient(containerName);
        }

        /// <summary>
        /// Creates a container if it doesn't exist
        /// </summary>
        /// <param name="containerName">The name of the container to create</param>
        /// <returns>True if container was created or already exists</returns>
        public async Task<bool> EnsureContainerExistsAsync(string containerName)
        {
            try
            {
                var containerClient = GetContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Container '{ContainerName}' ensured to exist", containerName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure container '{ContainerName}' exists", containerName);
                return false;
            }
        }

        /// <summary>
        /// Gets the BlobServiceClient for advanced operations
        /// </summary>
        public BlobServiceClient BlobServiceClient => _blobServiceClient;

        /// <summary>
        /// Gets a table client for the specified table name
        /// </summary>
        /// <param name="tableName">The name of the table</param>
        /// <returns>TableClient instance</returns>
        public TableClient GetTableClient(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

            return _tableServiceClient.GetTableClient(tableName);
        }

        /// <summary>
        /// Creates a table if it doesn't exist
        /// </summary>
        /// <param name="tableName">The name of the table to create</param>
        /// <returns>True if table was created or already exists</returns>
        public async Task<bool> EnsureTableExistsAsync(string tableName)
        {
            try
            {
                var tableClient = GetTableClient(tableName);
                await tableClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Table '{TableName}' ensured to exist", tableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure table '{TableName}' exists", tableName);
                return false;
            }
        }

        /// <summary>
        /// Gets the TableServiceClient for advanced operations
        /// </summary>
        public TableServiceClient TableServiceClient => _tableServiceClient;
    }
}
