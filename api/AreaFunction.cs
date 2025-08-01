using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
using api.Models;
using System;
using System.Threading.Tasks;

namespace api;

/// <summary>
/// Azure Function for managing Area endpoints
/// </summary>
public class AreaFunction
{
    private readonly ILogger<AreaFunction> _logger;
    private readonly AreaService _areaService;
    private readonly StationService _stationService;

    public AreaFunction(ILogger<AreaFunction> logger, AreaService areaService, StationService stationService)
    {
        _logger = logger;
        _areaService = areaService;
        _stationService = stationService;
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve all areas
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>JSON array of areas</returns>
    [Function("GetAreas")]
    public async Task<IActionResult> GetAllAreas(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area")] HttpRequest req)
    {
        try
        {
            _logger.LogInformation("Processing request to get all areas");

            var areas = await _areaService.GetAllAreasAsync();

            _logger.LogInformation("Successfully returned {Count} areas", areas.Count);

            return new OkObjectResult(areas);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get all areas: {Message}", ex.Message);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get all areas: {Message}", ex.Message);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting all areas");
            return new StatusCodeResult(500); // Internal Server Error
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve a specific area by ID
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Area ID from route</param>
    /// <returns>JSON object of the area or 404 if not found</returns>
    [Function("GetAreaById")]
    public async Task<IActionResult> GetAreaById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{id}")] HttpRequest req,
        string id)
    {
        try
        {
            _logger.LogInformation("Processing request to get area with ID: {AreaId}", id);

            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            var area = await _areaService.GetAreaByIdAsync(id);

            if (area == null)
            {
                _logger.LogWarning("Area with ID {AreaId} not found", id);
                return new NotFoundObjectResult(new { error = $"Area with ID '{id}' not found" });
            }

            _logger.LogInformation("Successfully returned area with ID: {AreaId}", id);

            return new OkObjectResult(area);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get area by ID: {Message}", ex.Message);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get area by ID: {Message}", ex.Message);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting area by ID: {AreaId}", id);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve all stations for a specific area
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Area ID from route</param>
    /// <returns>JSON array of stations for the specified area</returns>
    [Function("GetStationsByAreaId")]
    public async Task<IActionResult> GetStationsByAreaId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{id}/station")] HttpRequest req,
        string id)
    {
        try
        {
            _logger.LogInformation("Processing request to get stations for area ID: {AreaId}", id);

            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            var stations = await _stationService.GetStationsByAreaIdAsync(id);

            _logger.LogInformation("Successfully returned {Count} stations for area ID: {AreaId}", stations.Count, id);

            return new OkObjectResult(stations);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get stations by area ID: {Message}", ex.Message);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get stations by area ID: {Message}", ex.Message);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting stations for area ID: {AreaId}", id);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }
}
