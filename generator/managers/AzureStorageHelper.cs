using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Generator.Managers;

public static class AzureStorageHelper
{
    public static ClientSecretCredential GetAzureCredential(IConfiguration? configuration)
    {
        var tenantId = configuration?.GetSection("AppSettings")[ConfigurationKeys.TenantId];
        var clientId = configuration?.GetSection("AppSettings")[ConfigurationKeys.ClientId];
        var clientSecret = configuration?.GetSection("AppSettings")[ConfigurationKeys.ClientSecret];

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Azure AD credentials (tenantId, clientId, clientSecret) not configured");
        }

        return new ClientSecretCredential(tenantId, clientId, clientSecret);
    }

    public static TableServiceClient? CreateTableServiceClient(IConfiguration? configuration)
    {
        try
        {
            var tableUri = configuration?.GetSection("AppSettings")[ConfigurationKeys.TableUri];
            if (string.IsNullOrWhiteSpace(tableUri))
            {
                Console.WriteLine("❌ Table storage URI not configured");
                return null;
            }

            var credential = GetAzureCredential(configuration);
            return new TableServiceClient(new Uri(tableUri), credential);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to create Azure Table Storage client: {ex.Message}");
            return null;
        }
    }

    public static BlobServiceClient? CreateBlobServiceClient(IConfiguration? configuration)
    {
        try
        {
            var blobUri = configuration?.GetSection("AppSettings")[ConfigurationKeys.BlobUri];
            if (string.IsNullOrWhiteSpace(blobUri))
            {
                Console.WriteLine("❌ Blob storage URI not configured");
                return null;
            }

            var credential = GetAzureCredential(configuration);
            return new BlobServiceClient(new Uri(blobUri), credential);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to create Azure Blob Storage client: {ex.Message}");
            return null;
        }
    }
}
