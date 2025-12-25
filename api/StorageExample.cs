using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
namespace api;

public class StorageTester
{
    private readonly ILogger<StorageTester> _logger;
    private readonly StorageService _storageService;

    public StorageTester(ILogger<StorageTester> logger, StorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    [Function("StorageHealth")]
    public async Task<IActionResult> GetStorageHealth([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("Storage health check requested.");

        // Explicitly disable caching for health check endpoints
        req.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        req.HttpContext.Response.Headers["Pragma"] = "no-cache";
        req.HttpContext.Response.Headers["Expires"] = "0";

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



}

