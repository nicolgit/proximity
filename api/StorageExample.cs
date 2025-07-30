using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;

namespace api;

public class StorageExample
{
    private readonly ILogger<StorageExample> _logger;
    private readonly StorageService _storageService;

    public StorageExample(ILogger<StorageExample> logger, StorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    [Function("StorageHealth")]
    public async Task<IActionResult> GetStorageHealth([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("Storage health check requested.");

        try
        {
            var isHealthy = await _storageService.TestConnectionAsync();

            var healthStatus = new
            {
                Service = "Azure Storage",
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Timestamp = System.DateTime.UtcNow,
                Details = isHealthy ? "Connection successful" : "Connection failed"
            };

            return new OkObjectResult(healthStatus);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error checking storage health");

            var errorStatus = new
            {
                Service = "Azure Storage",
                Status = "Error",
                Timestamp = System.DateTime.UtcNow,
                Details = ex.Message
            };

            return new ObjectResult(errorStatus) { StatusCode = 500 };
        }
    }

    [Function("CreateContainer")]
    public async Task<IActionResult> CreateContainer([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("Container creation requested.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateContainerRequest>(requestBody);

            if (string.IsNullOrWhiteSpace(request?.ContainerName))
            {
                return new BadRequestObjectResult(new { Error = "ContainerName is required" });
            }

            var success = await _storageService.EnsureContainerExistsAsync(request.ContainerName);

            var response = new
            {
                ContainerName = request.ContainerName,
                Created = success,
                Timestamp = System.DateTime.UtcNow
            };

            return new OkObjectResult(response);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error creating container");
            return new ObjectResult(new { Error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("UploadText")]
    public async Task<IActionResult> UploadText([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("Text upload requested.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UploadTextRequest>(requestBody);

            if (string.IsNullOrWhiteSpace(request?.ContainerName) ||
                string.IsNullOrWhiteSpace(request?.BlobName) ||
                string.IsNullOrWhiteSpace(request?.Content))
            {
                return new BadRequestObjectResult(new { Error = "ContainerName, BlobName, and Content are required" });
            }

            // Ensure container exists
            await _storageService.EnsureContainerExistsAsync(request.ContainerName);

            // Get blob client and upload content
            var containerClient = _storageService.GetContainerClient(request.ContainerName);
            var blobClient = containerClient.GetBlobClient(request.BlobName);

            var contentBytes = Encoding.UTF8.GetBytes(request.Content);
            await blobClient.UploadAsync(new MemoryStream(contentBytes), overwrite: true);

            var response = new
            {
                ContainerName = request.ContainerName,
                BlobName = request.BlobName,
                Size = contentBytes.Length,
                Uploaded = true,
                Timestamp = System.DateTime.UtcNow
            };

            return new OkObjectResult(response);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error uploading text");
            return new ObjectResult(new { Error = ex.Message }) { StatusCode = 500 };
        }
    }
}

public class CreateContainerRequest
{
    public string ContainerName { get; set; } = string.Empty;
}

public class UploadTextRequest
{
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
