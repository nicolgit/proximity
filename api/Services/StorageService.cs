using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using api.Models;

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

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
        {
            _logger = logger;
            var blobUri = configuration["blobUri"];
            var tableUri = configuration["tableUri"];
            var tenantId = configuration["tenantId"];
            var clientId = configuration["clientId"];
            var clientSecret = configuration["clientSecret"];

            try
            {
                // Validate required configuration values
                if (string.IsNullOrWhiteSpace(blobUri))
                    throw new ArgumentException("blobUri configuration is required");
                if (string.IsNullOrWhiteSpace(tableUri))
                    throw new ArgumentException("tableUri configuration is required");
                if (string.IsNullOrWhiteSpace(tenantId))
                    throw new ArgumentException("tenantId configuration is required");
                if (string.IsNullOrWhiteSpace(clientId))
                    throw new ArgumentException("clientId configuration is required");
                if (string.IsNullOrWhiteSpace(clientSecret))
                    throw new ArgumentException("clientSecret configuration is required");

                var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                _blobServiceClient = new BlobServiceClient(new Uri(blobUri), credential);
                _tableServiceClient = new TableServiceClient(new Uri(tableUri), credential);
                _logger.LogInformation("Initialized BlobServiceClient and TableServiceClient with Azure AD credentials");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Storage clients");
                throw;
            }
        }

        /// <summary>
        /// Tests the connection to Azure Storage by verifying Storage Blob Data Reader and Storage Table Data Reader permissions
        /// </summary>
        /// <returns>True if connection is successful and permissions are verified, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Azure Storage connection and permissions...");

                // Test Storage Blob Data Reader permission
                bool blobPermissionTest = await TestBlobDataReaderPermissionAsync();
                if (!blobPermissionTest)
                {
                    _logger.LogError("Storage Blob Data Reader permission test failed");
                    return false;
                }

                // Test Storage Table Data Reader permission
                bool tablePermissionTest = await TestTableDataReaderPermissionAsync();
                if (!tablePermissionTest)
                {
                    _logger.LogError("Storage Table Data Reader permission test failed");
                    return false;
                }

                _logger.LogInformation("Azure Storage connection test successful. Both Blob and Table permissions verified.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Storage connection test failed: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Tests Storage Blob Data Reader permission by verifying the isochrone container exists and is not empty
        /// </summary>
        /// <returns>True if permission is verified, false otherwise</returns>
        private async Task<bool> TestBlobDataReaderPermissionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Storage Blob Data Reader permission...");
                
                // Get the isochrone container client
                var containerClient = GetContainerClient("isochrone");
                
                // Check if container exists
                var containerExists = await containerClient.ExistsAsync();
                if (!containerExists.Value)
                {
                    _logger.LogError("Isochrone container does not exist");
                    return false;
                }

                _logger.LogInformation("Isochrone container exists, checking for blobs...");

                // Attempt to list blobs in the isochrone container - this requires Storage Blob Data Reader permission
                var blobs = containerClient.GetBlobsAsync(prefix: null);
                int blobCount = 0;
                
                await foreach (var blob in blobs)
                {
                    blobCount++;
                    _logger.LogInformation("Successfully accessed blob: {BlobName} in isochrone container", blob.Name);
                    break; // Only need to verify we can read one blob
                }

                if (blobCount == 0)
                {
                    _logger.LogError("Isochrone container exists but is empty");
                    return false;
                }

                _logger.LogInformation("Storage Blob Data Reader permission verified. Isochrone container has at least {BlobCount} blob(s)", blobCount);
                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError("Access denied for blob storage. Missing 'Storage Blob Data Reader' role assignment. Error: {ErrorMessage}", ex.Message);
                return false;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Isochrone container not found. Error: {ErrorMessage}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Storage Blob Data Reader permission: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Tests Storage Table Data Reader permission by attempting to query the area table for at least 1 row
        /// </summary>
        /// <returns>True if permission is verified, false otherwise</returns>
        private async Task<bool> TestTableDataReaderPermissionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Storage Table Data Reader permission...");
                
                // Attempt to query the area table - this requires Storage Table Data Reader permission
                var tableClient = GetTableClient("area");
                var entities = tableClient.QueryAsync<AreaEntity>(maxPerPage: 1);
                
                int rowCount = 0;
                await foreach (var entity in entities)
                {
                    rowCount++;
                    _logger.LogInformation("Successfully accessed area table. Found entity with PartitionKey: {PartitionKey}, RowKey: {RowKey}", 
                        entity.PartitionKey, entity.RowKey);
                    break; // Only need to verify we can read one row
                }

                if (rowCount == 0)
                {
                    _logger.LogWarning("Area table exists but contains no data");
                }

                _logger.LogInformation("Storage Table Data Reader permission verified. Area table has {RowCount} rows (checked first row only)", rowCount);
                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError("Access denied for table storage. Missing 'Storage Table Data Reader' role assignment. Error: {ErrorMessage}", ex.Message);
                return false;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Area table not found. Error: {ErrorMessage}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Storage Table Data Reader permission: {ErrorMessage}", ex.Message);
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

        /// <summary>
        /// Retrieves blob content as string from the specified container and blob path
        /// </summary>
        /// <param name="containerName">The name of the container</param>
        /// <param name="blobPath">The path to the blob</param>
        /// <returns>The blob content as string, or null if blob doesn't exist</returns>
        public async Task<string?> GetBlobContentAsync(string containerName, string blobPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(containerName))
                    throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

                if (string.IsNullOrWhiteSpace(blobPath))
                    throw new ArgumentException("Blob path cannot be null or empty", nameof(blobPath));

                var containerClient = GetContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobPath);

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    _logger.LogWarning("Blob '{BlobPath}' not found in container '{ContainerName}'", blobPath, containerName);
                    return null;
                }

                // Download blob content
                var response = await blobClient.DownloadContentAsync();
                var content = response.Value.Content.ToString();

                _logger.LogInformation("Successfully retrieved blob '{BlobPath}' from container '{ContainerName}', size: {Size} bytes", 
                    blobPath, containerName, content.Length);

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve blob '{BlobPath}' from container '{ContainerName}': {ErrorMessage}", 
                    blobPath, containerName, ex.Message);
                throw;
            }
        }
    }
}
