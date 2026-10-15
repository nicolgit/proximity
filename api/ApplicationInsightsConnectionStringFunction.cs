using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;

namespace api;

public class ApplicationInsightsConnectionStringFunction
{
    private readonly ILogger<ApplicationInsightsConnectionStringFunction> _logger;
    private readonly IConfiguration _configuration;

    public ApplicationInsightsConnectionStringFunction(
        ILogger<ApplicationInsightsConnectionStringFunction> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("GetApplicationInsightsConnectionString")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "application-insights/connection-string")] HttpRequest req)
    {
        _logger.LogInformation("Application Insights connection string requested.");

        var connectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Application Insights connection string not found in configuration.");
            return new NotFoundObjectResult(new { error = "Application Insights connection string not found" });
        }

        return new OkObjectResult(new ApplicationInsightsConnectionStringResponse(connectionString));
    }
}

/// <summary>Contains the Application Insights connection string for browser telemetry.</summary>
public sealed record ApplicationInsightsConnectionStringResponse(
    [property: JsonPropertyName("connectionString")] string ConnectionString);
