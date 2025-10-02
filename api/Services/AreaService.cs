using api.Models;
using api.Services;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace api.Services;

/// <summary>
/// Service for managing Area entities in Azure Table Storage
/// </summary>
public class AreaService
{
    private readonly StorageService _storageService;
    private readonly ILogger<AreaService> _logger;
    private readonly string _tableName = "area"; // Table name for areas

    public AreaService(StorageService storageService, ILogger<AreaService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all areas from the table storage
    /// </summary>
    /// <returns>List of AreaDto objects</returns>
    public async Task<List<AreaDto>> GetAllAreasAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all areas from table storage");

            var tableClient = _storageService.GetTableClient(_tableName);

            // Query all entities with partition key "area"
            var queryResults = tableClient.QueryAsync<AreaEntity>(
                filter: $"PartitionKey eq 'area'",
                maxPerPage: 1000 // Adjust based on expected data size
            );

            var areas = new List<AreaDto>();

            await foreach (var entity in queryResults)
            {
                areas.Add(new AreaDto
                {
                    Id = entity.RowKey ?? string.Empty,
                    Name = entity.DisplayName ?? entity.Name ?? string.Empty,
                    Latitude = entity.Latitude,
                    Longitude = entity.Longitude,
                    Diameter = entity.DiameterMeters
                });
            }

            _logger.LogInformation("Successfully retrieved {Count} areas", areas.Count);
            return areas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve areas from table storage");
            throw new InvalidOperationException("Failed to retrieve areas from storage", ex);
        }
    }

    /// <summary>
    /// Retrieves a specific area by ID
    /// </summary>
    /// <param name="id">The ID (RowKey) of the area</param>
    /// <returns>AreaDto or null if not found</returns>
    public async Task<AreaDto?> GetAreaByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Area ID cannot be null or empty", nameof(id));

            _logger.LogInformation("Retrieving area with ID: {AreaId}", id);

            var tableClient = _storageService.GetTableClient(_tableName);

            // Get specific entity
            var response = await tableClient.GetEntityIfExistsAsync<AreaEntity>("area", id);

            if (!response.HasValue || response.Value == null)
            {
                _logger.LogWarning("Area with ID {AreaId} not found", id);
                return null;
            }

            var entity = response.Value;
            var areaDto = new AreaDto
            {
                Id = entity.RowKey ?? string.Empty,
                Name = entity.DisplayName ?? entity.Name ?? string.Empty,
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,
                Diameter = entity.DiameterMeters
            };

            _logger.LogInformation("Successfully retrieved area with ID: {AreaId}", id);
            return areaDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve area with ID: {AreaId}", id);
            throw new InvalidOperationException($"Failed to retrieve area with ID: {id}", ex);
        }
    }

    /// <summary>
    /// Retrieves pre-generated isochrone data for a specific area
    /// </summary>
    /// <param name="areaId">The area ID</param>
    /// <param name="time">The time parameter (5, 10, 15, 20, or 30 minutes)</param>
    /// <returns>Pre-generated isochrone data as JSON string, or null if file not found</returns>
    /// <exception cref="FileNotFoundException">Thrown when the isochrone file is not found</exception>
    public async Task<string?> GetAreaIsochroneAsync(string areaId, string time)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(areaId))
                throw new ArgumentException("Area ID cannot be null or empty", nameof(areaId));

            if (string.IsNullOrWhiteSpace(time))
                throw new ArgumentException("Time parameter cannot be null or empty", nameof(time));

            _logger.LogInformation("Retrieving pre-generated isochrone for area: {AreaId}, time: {Time}", areaId, time);

            // First, verify the area exists
            var area = await GetAreaByIdAsync(areaId);
            if (area == null)
            {
                _logger.LogWarning("Area with ID {AreaId} not found", areaId);
                throw new FileNotFoundException($"Area with ID {areaId} not found");
            }

            // Construct the blob path for the pre-generated isochrone file
            var containerName = "isochrone";
            var blobPath = $"{areaId}/{time}min.json";

            _logger.LogDebug("Looking for pre-generated isochrone file at: {BlobPath}", blobPath);

            // Retrieve the pre-generated isochrone data
            var isochroneData = await _storageService.GetBlobContentAsync(containerName, blobPath);

            if (isochroneData == null)
            {
                _logger.LogWarning("Pre-generated isochrone file not found for area: {AreaId}, time: {Time}, path: {BlobPath}", areaId, time, blobPath);
                throw new FileNotFoundException($"Isochrone data not found for area {areaId} and time {time} minutes");
            }

            _logger.LogInformation("Successfully retrieved pre-generated isochrone for area: {AreaId}, time: {Time}", areaId, time);
            return isochroneData;
        }
        catch (FileNotFoundException)
        {
            // Re-throw FileNotFoundException as-is for proper HTTP 404 handling
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pre-generated isochrone for area: {AreaId}, time: {Time}", areaId, time);
            throw new InvalidOperationException($"Failed to retrieve isochrone for area: {areaId}", ex);
        }
    }
}
