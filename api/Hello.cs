using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace api;

public class Hello
{
    private readonly ILogger<Hello> _logger;

    public Hello(ILogger<Hello> logger)
    {
        _logger = logger;
    }

    [Function("Hello")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var response = new
        {
            message = "Hello World!",
            timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            version = "1.0.0"
        };

        return new OkObjectResult(response);
    }
}
