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

    public static TableServiceClient CreateTableServiceClient(IConfiguration? configuration)
    {
        var tableUri = configuration?.GetSection("AppSettings")[ConfigurationKeys.TableUri];
        if (string.IsNullOrWhiteSpace(tableUri))
        {
            throw new InvalidOperationException("Table storage URI not configured");
        }

        var credential = GetAzureCredential(configuration);
        return new TableServiceClient(new Uri(tableUri), credential);
    }

    public static BlobServiceClient CreateBlobServiceClient(IConfiguration? configuration)
    {
        var blobUri = configuration?.GetSection("AppSettings")[ConfigurationKeys.BlobUri];
        if (string.IsNullOrWhiteSpace(blobUri))
        {
            throw new InvalidOperationException("Blob storage URI not configured");
        }

        var credential = GetAzureCredential(configuration);
        return new BlobServiceClient(new Uri(blobUri), credential);
    }
}
