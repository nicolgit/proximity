using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace api;

public class MapKeyFunction
{
    private readonly ILogger<MapKeyFunction> _logger;
    private readonly IConfiguration _configuration;

    public MapKeyFunction(ILogger<MapKeyFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("GetMapKey")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "map/key")] HttpRequest req)
    {
        _logger.LogInformation("Map key requested.");

        try
        {
            var mapKey = _configuration["mapKey"];

            if (string.IsNullOrEmpty(mapKey))
            {
                _logger.LogWarning("Map key not found in configuration.");
                return new NotFoundObjectResult(new { error = "Map key not found" });
            }

            var response = new
            {
                mapKey = mapKey
            };

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving map key.");
            return new StatusCodeResult(500);
        }
    }
}