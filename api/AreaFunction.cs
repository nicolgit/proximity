using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api.Services;
using api.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{country}/{id}")] HttpRequest req,
        string country, string id)
    {
        try
        {
            _logger.LogInformation("Processing request to get area with ID: {country}/{AreaId}", country, id);

            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            var area = await _areaService.GetAreaByIdAsync(country, id);

            if (area == null)
            {
                _logger.LogWarning("Area with ID {country}/{AreaId} not found", country, id);
                return new NotFoundObjectResult(new { error = $"Area with ID '{id}' not found" });
            }

            // Generate ETag and check for conditional requests
            var etag = CdnResponseService.GenerateETag(area);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Area data not modified, returning 304 for ID: {country}/{AreaId}", country, id);
                CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, area);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
            }

            // Configure CDN-friendly headers for cacheable area data
            CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, area);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully returned area with ID: {country}/{AreaId}", country, id);

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{country}/{id}/station")] HttpRequest req,
        string country,
        string id)
    {
        try
        {
            _logger.LogInformation("Processing request to get stations for area ID: {AreaId}", id);

            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            var stations = await _stationService.GetStationsByAreaIdAsync(country, id);

            // Generate ETag and check for conditional requests
            var etag = CdnResponseService.GenerateETag(stations);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Stations data not modified, returning 304 for area ID: {AreaId}", id);
                CdnResponseService.ConfigureCacheableResponse(req.HttpContext.Response, stations);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
            }

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
    /// <param name="country">Country from route</param>
    /// <param name="id">Area ID from route</param>
    /// <param name="stationid">Station ID from route</param>
    /// <param name="time">Time parameter (10, 15, 20, or 30 minutes) from route</param>
    /// <returns>JSON content of the isochrone blob or 404 if not found</returns>
    [Function("GetIsochroneData")]
    public async Task<IActionResult> GetIsochroneData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{country}/{id}/station/{stationid}/isochrone/{time}")] HttpRequest req,
        string country,
        string id,
        string stationid,
        string time)
    {
        try
        {
            _logger.LogInformation("Processing request to get isochrone data for area: {Country}/{AreaId}, station: {StationId}, time: {Time}",
                country, id, stationid, time);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(country))
            {
                return new BadRequestObjectResult(new { error = "Country is required" });
            }
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

            // Construct blob path: isochrone/{country}/{id}/{stationid}/{time}min.json
            var blobPath = $"{country}/{id}/{stationid}/{time}min.json";
            var containerName = "isochrone";

            // Retrieve blob content
            var blobContent = await _storageService.GetBlobContentAsync(containerName, blobPath);

            if (blobContent == null)
            {
                _logger.LogWarning("Isochrone data not found for area: {Country}/{AreaId}, station: {StationId}, time: {Time}", 
                    country, id, stationid, time);
                return new NotFoundObjectResult(new { error = $"Isochrone data not found for the specified parameters" });
            }

            _logger.LogInformation("Successfully retrieved isochrone data for area: {Country}/{AreaId}, station: {StationId}, time: {Time}", 
                country, id, stationid, time);

            // Check for conditional requests (304 Not Modified)
            var etag = CdnResponseService.GenerateETag(blobContent);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Isochrone data not modified, returning 304");
                CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, blobContent);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
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
    /// <param name="country">Country from route</param>
    /// <param name="id">Area ID from route</param>
    /// <param name="stationType">Station type (station, trolleybus, halt) from route</param>
    /// <param name="time">Time parameter (5, 10, 15, 20, or 30 minutes) from route</param>
    /// <returns>JSON content of the combined isochrone data or 404 if not found</returns>
    [Function("GetAreaIsochroneStationData")]
    public async Task<IActionResult> GetAreaIsochroneStationData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{country}/{id}/isochrone/{stationType}/{time}")] HttpRequest req,
        string country,
        string id,
        string stationType,
        string time)
    {
        try
        {
            _logger.LogInformation("Processing request to get area-level isochrone data for area: {Country}/{AreaId}, station-type: {StationType}, time: {Time}", 
                country, id, stationType, time);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(country))
            {
                return new BadRequestObjectResult(new { error = "Country is required" });
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult(new { error = "Area ID is required" });
            }

            if (string.IsNullOrWhiteSpace(stationType))
            {
                return new BadRequestObjectResult(new { error = "Station type is required" });
            }

            if (string.IsNullOrWhiteSpace(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter is required" });
            }

            // Validate station type parameter - must be station, trolleybus, or halt
            var allowedStationTypes = new[] { "station", "trolleybus", "tram_stop" };
            if (!allowedStationTypes.Contains(stationType))
            {
                return new BadRequestObjectResult(new { error = "Station type must be one of: station, trolleybus, halt" });
            }

            // Validate time parameter - must be 5, 10, 15, 20, or 30
            var allowedTimes = new[] { "5", "10", "15", "20", "30" };
            if (!allowedTimes.Contains(time))
            {
                return new BadRequestObjectResult(new { error = "Time parameter must be one of: 5, 10, 15, 20, 30" });
            }

            // Get pre-generated isochrone data for the area
            var areaIsochroneData = await _areaService.GetAreaIsochroneAsync(country, id, stationType, time);

            if (areaIsochroneData == null)
            {
                _logger.LogWarning("Area-level isochrone data not found for area: {country}/{AreaId}, station-type: {StationType}, time: {Time}", country, id, stationType, time);
                return new NotFoundObjectResult(new { error = $"Area-level isochrone data not found for the specified parameters" });
            }

            // Check for conditional requests (304 Not Modified)
            var etag = CdnResponseService.GenerateETag(areaIsochroneData);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Area isochrone data not modified, returning 304");
                CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
            }

            // Configure CDN-friendly headers for area isochrone data
            CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully retrieved area-level isochrone data for area: {country}/{AreaId}, station-type: {StationType}, time: {Time}", country, id, stationType, time);

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
            _logger.LogWarning(ex, "Area isochrone file not found for area: {country}/{AreaId}, station-type: {StationType}, time: {Time}", country, id, stationType, time);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new NotFoundObjectResult(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get area isochrone data: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get area isochrone data: {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting area isochrone data for area: {country}/{AreaId}, station-type: {StationType}, time: {Time}", 
                country, id, stationType, time);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }

    /// <summary>
    /// HTTP GET endpoint to retrieve combined isochrone data for all stations in an area (simplified route)
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="country">Country from route</param>
    /// <param name="id">Area ID from route</param>
    /// <param name="time">Time parameter (5, 10, 15, 20, or 30 minutes) from route</param>
    /// <returns>JSON content of the combined isochrone data or 404 if not found</returns>
    [Function("GetAreaIsochroneData")]
    public async Task<IActionResult> GetAreaIsochroneData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "area/{country}/{id}/isochrone/{time}")] HttpRequest req,
        string country,
        string id,
        string time)
    {
        try
        {
            _logger.LogInformation("Processing request to get area-level isochrone data (simplified) for area: {Country}/{AreaId}, time: {Time}", 
                country, id, time);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(country))
            {
                return new BadRequestObjectResult(new { error = "Country is required" });
            }

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

            // Get pre-generated isochrone data for the area (without station type)
            var areaIsochroneData = await _areaService.GetAreaIsochroneAsync(country, id, time);

            if (areaIsochroneData == null)
            {
                _logger.LogWarning("Area-level isochrone data not found for area: {country}/{AreaId}, time: {Time}", country, id, time);
                return new NotFoundObjectResult(new { error = $"Area-level isochrone data not found for the specified parameters" });
            }

            // Check for conditional requests (304 Not Modified)
            var etag = CdnResponseService.GenerateETag(areaIsochroneData);
            if (CdnResponseService.IsNotModified(req, etag))
            {
                _logger.LogInformation("Area isochrone data not modified, returning 304");
                CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
                CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);
                return CdnResponseService.CreateNotModifiedResponse(etag);
            }

            // Configure CDN-friendly headers for area isochrone data
            CdnResponseService.ConfigureIsochroneResponse(req.HttpContext.Response, areaIsochroneData);
            CdnResponseService.ConfigureCorsHeaders(req.HttpContext.Response);

            _logger.LogInformation("Successfully retrieved area-level isochrone data (simplified) for area: {country}/{AreaId}, time: {Time}", country, id, time);

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
            _logger.LogWarning(ex, "Area isochrone file not found for area: {country}/{AreaId}, time: {Time}", country, id, time);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new NotFoundObjectResult(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request for get area isochrone data (simplified): {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Storage operation failed for get area isochrone data (simplified): {Message}", ex.Message);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(503); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting area isochrone data (simplified) for area: {country}/{AreaId}, time: {Time}", 
                country, id, time);
            CdnResponseService.ConfigureNoCacheResponse(req.HttpContext.Response);
            return new StatusCodeResult(500); // Internal Server Error
        }
    }
}
