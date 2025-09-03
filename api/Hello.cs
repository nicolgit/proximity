using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
using System.Threading.Tasks;

namespace api;

public class Hello
{
    private readonly ILogger<Hello> _logger;
    private readonly StorageService _storageService;

    public Hello(ILogger<Hello> logger, StorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    [Function("Hello")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        // Test storage connection as part of the function execution
        var storageHealthy = await _storageService.TestConnectionAsync();

        var response = new
        {
            Message = "Welcome to Azure Functions!",
            StorageConnection = storageHealthy ? "Healthy" : "Unhealthy",
            Timestamp = System.DateTime.UtcNow
        };

        return new OkObjectResult(response);
    }
}
