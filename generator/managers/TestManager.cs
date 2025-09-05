using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Generator.Managers;

public static class TestManager
{
    public static async Task<bool> TestAzureStorageConnectionAsync(ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Testing Azure Storage permissions: Storage Blob Data Contributor and Storage Table Data Contributor...");
            Console.WriteLine("🔍 Testing Azure Storage permissions...");

            // Create BlobServiceClient and TableServiceClient using AzureStorageHelper
            BlobServiceClient blobServiceClient;
            TableServiceClient tableServiceClient;
            
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
                tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            }
            catch (InvalidOperationException ex)
            {
                logger?.LogInformation("Azure AD credentials or storage URIs not configured. Skipping storage test.");
                Console.WriteLine("⚠️ Azure configuration not complete - storage test skipped");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine();
                return false; // This is a critical failure since we need these for the app to work
            }

            // Test 1: Verify Storage Blob Data Contributor permissions
            logger?.LogInformation("Testing Storage Blob Data Contributor permissions...");
            Console.WriteLine("  📁 Testing Storage Blob Data Contributor permissions...");
            try
            {
                var testContainerName = $"permission-test-{Guid.NewGuid():N}";
                var containerClient = blobServiceClient.GetBlobContainerClient(testContainerName);
                
                // Test container creation (requires Storage Blob Data Contributor)
                await containerClient.CreateAsync();
                logger?.LogInformation("✓ Container creation successful - Storage Blob Data Contributor verified");
                
                // Test blob upload (requires Storage Blob Data Contributor)
                var blobClient = containerClient.GetBlobClient("test-blob.txt");
                await blobClient.UploadAsync(new BinaryData("Test data for permission verification"));
                logger?.LogInformation("✓ Blob upload successful");
                
                // Test blob read (requires Storage Blob Data Contributor)
                var downloadResult = await blobClient.DownloadContentAsync();
                logger?.LogInformation("✓ Blob read successful");
                
                // Test blob delete (requires Storage Blob Data Contributor)
                await blobClient.DeleteAsync();
                logger?.LogInformation("✓ Blob delete successful");
                
                // Test container deletion (requires Storage Blob Data Contributor)
                await containerClient.DeleteAsync();
                logger?.LogInformation("✓ Container deletion successful - Storage Blob Data Contributor verified");
                
                Console.WriteLine("    ✅ Storage Blob Data Contributor permissions verified");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                logger?.LogError("Storage Blob Data Contributor permissions test failed - access denied");
                Console.WriteLine("    ❌ Storage Blob Data Contributor permissions missing");
                Console.WriteLine("       The service principal needs 'Storage Blob Data Contributor' role assignment");
                return false;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Storage Blob Data Contributor permissions test failed");
                Console.WriteLine("    ❌ Storage Blob Data Contributor test failed");
                Console.WriteLine($"       Error: {ex.Message}");
                return false;
            }

            // Test 2: Verify Storage Table Data Contributor permissions
            logger?.LogInformation("Testing Storage Table Data Contributor permissions...");
            Console.WriteLine("  📊 Testing Storage Table Data Contributor permissions...");
            try
            {
                var testTableName = $"permissionTest{Guid.NewGuid():N}";
                var tableClient = tableServiceClient.GetTableClient(testTableName);
                
                // Test table creation (requires Storage Table Data Contributor)
                await tableClient.CreateAsync();
                logger?.LogInformation("✓ Table creation successful - Storage Table Data Contributor verified");

                // Test entity creation (requires Storage Table Data Contributor)
                var testEntity = new TableEntity("testPartition", "testRow")
                {
                    ["TestProperty"] = "Permission verification test",
                    ["Timestamp"] = DateTimeOffset.UtcNow
                };
                await tableClient.AddEntityAsync(testEntity);
                logger?.LogInformation("✓ Entity creation successful");

                // Test entity read (requires Storage Table Data Contributor)
                var retrievedEntity = await tableClient.GetEntityAsync<TableEntity>("testPartition", "testRow");
                logger?.LogInformation("✓ Entity read successful");

                // Test entity update (requires Storage Table Data Contributor)
                testEntity["TestProperty"] = "Updated value";
                await tableClient.UpsertEntityAsync(testEntity);
                logger?.LogInformation("✓ Entity update successful");

                // Test entity delete (requires Storage Table Data Contributor)
                await tableClient.DeleteEntityAsync("testPartition", "testRow");
                logger?.LogInformation("✓ Entity delete successful");

                // Test table deletion (requires Storage Table Data Contributor)
                await tableClient.DeleteAsync();
                logger?.LogInformation("✓ Table deletion successful - Storage Table Data Contributor verified");
                
                Console.WriteLine("    ✅ Storage Table Data Contributor permissions verified");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                logger?.LogError("Storage Table Data Contributor permissions test failed - access denied");
                Console.WriteLine("    ❌ Storage Table Data Contributor permissions missing");
                Console.WriteLine("       The service principal needs 'Storage Table Data Contributor' role assignment");
                return false;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Storage Table Data Contributor permissions test failed");
                Console.WriteLine("    ❌ Storage Table Data Contributor test failed");
                Console.WriteLine($"       Error: {ex.Message}");
                return false;
            }

            logger?.LogInformation("All Azure Storage permissions verified successfully");
            Console.WriteLine("✅ Azure Storage permission verification complete!");
            Console.WriteLine("   ✓ Storage Blob Data Contributor - Verified");
            Console.WriteLine("   ✓ Storage Table Data Contributor - Verified");
            Console.WriteLine();
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to connect to Azure Storage. This is a critical error.");
            Console.WriteLine("❌ Azure Storage connection test failed (check configuration)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
    }

    public static async Task<bool> TestMapBoxApiKeyAsync(ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            var mapBoxKey = configuration?.GetSection("AppSettings")[ConfigurationKeys.MapBoxSubscriptionKey];

            if (string.IsNullOrWhiteSpace(mapBoxKey) || mapBoxKey.Contains("<") || mapBoxKey.Contains(">"))
            {
                logger?.LogInformation("No valid MapBox API key configured. Skipping MapBox API test.");
                return false;
            }

            logger?.LogInformation("Testing MapBox API key...");

            // Test MapBox API with a simple account validation request
            // Using the MapBox Account API to validate the token
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://api.mapbox.com/tokens/v2?access_token={mapBoxKey}";

            logger?.LogDebug("Calling MapBox API: {Url}", url.Replace(mapBoxKey, "***"));

            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                logger?.LogInformation("Successfully validated MapBox API key");
                logger?.LogDebug("MapBox API response: {Response}", content);

                Console.WriteLine($"✓ MapBox API key validation successful!");
                Console.WriteLine($"  Status: {response.StatusCode}");
                Console.WriteLine();
                return true;
            }
            else
            {
                logger?.LogError("MapBox API key validation failed with status: {StatusCode}", response.StatusCode);
                Console.WriteLine($"❌ MapBox API key validation failed (Status: {response.StatusCode})");
                Console.WriteLine();
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError(ex, "Failed to connect to MapBox API. Check internet connection and API key.");
            Console.WriteLine("❌ MapBox API connection test failed (check network/key)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
        catch (TaskCanceledException ex)
        {
            logger?.LogError(ex, "MapBox API request timed out.");
            Console.WriteLine("❌ MapBox API request timed out");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to validate MapBox API key. This is a critical error.");
            Console.WriteLine("❌ MapBox API key validation failed (check configuration)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
    }
}
