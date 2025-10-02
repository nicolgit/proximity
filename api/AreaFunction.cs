using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
using api.Models;
using System;
using System.Linq;
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
    private readonly StorageService _storageService;

    public AreaFunction(ILogger<AreaFunction> logger, AreaService areaService, StationService stationService, StorageService storageService)
    {
        _logger = logger;
        _areaService = areaService;
        _stationService = stationService;
        _storageService = storageService;
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

            // Generate ETag and check for conditional requests
            var etag = CdnResponseService.GenerateETag(areas);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Areas data not modified, returning 304");
                CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, areas);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
            }

            // Configure CDN-friendly headers for cacheable area data
            CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, areas);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully returned {Count} areas", areas.Count);

            return new OkObjectResult(areas);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get all areas: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get all areas: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting all areas");
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
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

            // Configure CDN-friendly headers for cacheable area data
            CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, area);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully returned area with ID: {AreaId}", id);

            return new OkObjectResult(area);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get area by ID: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get area by ID: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting area by ID: {AreaId}", id);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
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

            // Configure CDN-friendly headers for cacheable station data
            CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, stations);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully returned {Count} stations for area ID: {AreaId}", stations.Count, id);

            return new OkObjectResult(stations);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get stations by area ID: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get stations by area ID: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting stations for area ID: {AreaId}", id);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve isochrone data for a specific station within an area
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Area ID from route</param>
    /// <param name="stationid">Station ID from route</param>
    /// <param name="time">Time parameter (10, 15, 20, or 30 minutes) from route</param>
    /// <returns>JSON content of the isochrone blob or 404 if not found</returns>
    [Function("GetIsochroneData")]
    public async Task<IActionResult> GetIsochroneData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{id}/station/{stationid}/isochrone/{time}")] HttpRequest req,
        string id,
        string stationid,
        string time)
    {
        try
        {
            _logger.LogInformation("Processing request to get isochrone data for area: {AreaId}, station: {StationId}, time: {Time}", 
                id, stationid, time);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            if (string.IsNullOrWhiteSpace(stationid))
            {
                return new BadRequestObjectResult(new { error = "Station ID is required" });
            }

            if (string.IsNullOrWhiteSpace(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter is required" });
            }

            // Validate time parameter - must be 5, 10, 15, 20, or 30
            var allowedTimes = new[] { "5", "10", "15", "20", "30" };
            if (!allowedTimes.Contains(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter must be one of: 5, 10, 15, 20, 30" });
            }

            // Construct blob path: isochrone/{id}/{stationid}/{time}min.json
            var blobPath = $"{id}/{stationid}/{time}min.json";
            var containerName = "isochrone";

            // Retrieve blob content
            var blobContent = await _storageService.GetBlobContentAsync(containerName, blobPath);

            if (blobContent == null)
            {
                _logger.LogWarning("Isochrone data not found for area: {AreaId}, station: {StationId}, time: {Time}", 
                    id, stationid, time);
                return new NotFoundObjectResult(new { error = $"Isochrone data not found for the specified parameters" });
            }

            _logger.LogInformation("Successfully retrieved isochrone data for area: {AreaId}, station: {StationId}, time: {Time}", 
                id, stationid, time);

            // Check for conditional requests (304 Not Modified)
            var etag = CdnResponseService.GenerateETag(blobContent);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Isochrone data not modified, returning 304");
                CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, blobContent);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return new StatusCodeResult(304);
            }

            // Configure CDN-friendly headers for isochrone data (long cache)
            CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, blobContent);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            // Return the blob content as JSON
            // Since the blob content is already JSON, we can return it directly
            return new ContentResult
            {
                Content = blobContent,
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get isochrone data: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get isochrone data: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting isochrone data for area: {AreaId}, station: {StationId}, time: {Time}", 
                id, stationid, time);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve combined isochrone data for all stations in an area
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">Area ID from route</param>
    /// <param name="time">Time parameter (5, 10, 15, 20, or 30 minutes) from route</param>
    /// <returns>JSON content of the combined isochrone data or 404 if not found</returns>
    [Function("GetAreaIsochroneData")]
    public async Task<IActionResult> GetAreaIsochroneData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{id}/isochrone/{time}")] HttpRequest req,
        string id,
        string time)
    {
        try
        {
            _logger.LogInformation("Processing request to get area-level isochrone data for area: {AreaId}, time: {Time}", 
                id, time);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            if (string.IsNullOrWhiteSpace(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter is required" });
            }

            // Validate time parameter - must be 5, 10, 15, 20, or 30
            var allowedTimes = new[] { "5", "10", "15", "20", "30" };
            if (!allowedTimes.Contains(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter must be one of: 5, 10, 15, 20, 30" });
            }

            // Get pre-generated isochrone data for the area
            var areaIsochroneData = await _areaService.GetAreaIsochroneAsync(id, time);

            if (areaIsochroneData == null)
            {
                _logger.LogWarning("Area-level isochrone data not found for area: {AreaId}, time: {Time}", id, time);
                return new NotFoundObjectResult(new { error = $"Area-level isochrone data not found for the specified parameters" });
            }

            // Check for conditional requests (304 Not Modified)
            var etag = CdnResponseService.GenerateETag(areaIsochroneData);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Area isochrone data not modified, returning 304");
                CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return new StatusCodeResult(304);
            }

            // Configure CDN-friendly headers for area isochrone data
            CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully retrieved area-level isochrone data for area: {AreaId}, time: {Time}", id, time);

            // Return the combined isochrone content as JSON
            return new ContentResult
            {
                Content = areaIsochroneData,
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Area isochrone file not found for area: {AreaId}, time: {Time}", id, time);
            return new NotFoundObjectResult(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get area isochrone data: {Message}", ex.Message);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get area isochrone data: {Message}", ex.Message);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting area isochrone data for area: {AreaId}, time: {Time}", 
                id, time);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }
}
