using api.Models;
using api.Services;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace api.Services;

/// <summary>
/// Service for managing Station entities in Azure Table Storage
/// </summary>
public class StationService
{
    private readonly StorageService _storageService;
    private readonly ILogger<StationService> _logger;
    private readonly string _tableName = "station"; // Table name for stations

    public StationService(StorageService storageService, ILogger<StationService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all stations for a specific area from table storage
    /// </summary>
    /// <param name="areaId">The area ID to filter stations by</param>
    /// <returns>List of StationDto objects</returns>
    public async Task<List<StationDto>> GetStationsByAreaIdAsync(string country,string areaId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(country))
                throw new ArgumentException("Country cannot be null or empty", nameof(country));

            if (string.IsNullOrWhiteSpace(areaId))
                throw new ArgumentException("Area ID cannot be null or empty", nameof(areaId));

            _logger.LogInformation("Retrieving stations for country: {Country}, area ID: {AreaId} from table storage", country, areaId);

            var tableClient = _storageService.GetTableClient(_tableName);

            var partitionKey = $"{country}-{areaId}";
            // Query all entities with partition key matching the area ID
            var queryResults = tableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{partitionKey}'",
                maxPerPage: 1000 // Adjust based on expected data size
            );

            var stations = new List<StationDto>();

            await foreach (var entity in queryResults)
            {
                stations.Add(new StationDto
                {
                    Id = entity.RowKey ?? string.Empty,
                    Name = entity.Name ?? string.Empty,
                    Latitude = entity.Latitude,
                    Longitude = entity.Longitude,
                    Type = entity.Railway ?? string.Empty,
                    WikipediaLink = entity.WikipediaLink
                });
            }

            _logger.LogInformation("Successfully retrieved {Count} stations for area ID: {AreaId}", stations.Count, areaId);
            return stations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve stations for area ID: {AreaId} from table storage", areaId);
            throw new InvalidOperationException($"Failed to retrieve stations for area ID: {areaId}", ex);
        }
    }

    /// <summary>
    /// Retrieves a specific station by area ID and station ID
    /// </summary>
    /// <param name="areaId">The area ID (PartitionKey)</param>
    /// <param name="stationId">The station ID (RowKey)</param>
    /// <returns>StationDto or null if not found</returns>
    public async Task<StationDto?> GetStationByIdAsync(string areaId, string stationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(areaId))
                throw new ArgumentException("Area ID cannot be null or empty", nameof(areaId));

            if (string.IsNullOrWhiteSpace(stationId))
                throw new ArgumentException("Station ID cannot be null or empty", nameof(stationId));

            _logger.LogInformation("Retrieving station with Area ID: {AreaId}, Station ID: {StationId}", areaId, stationId);

            var tableClient = _storageService.GetTableClient(_tableName);

            // Get specific entity
            var response = await tableClient.GetEntityIfExistsAsync<StationEntity>(areaId, stationId);

            if (!response.HasValue || response.Value == null)
            {
                _logger.LogWarning("Station with Area ID: {AreaId}, Station ID: {StationId} not found", areaId, stationId);
                return null;
            }

            var entity = response.Value;
            var stationDto = new StationDto
            {
                Id = entity.RowKey ?? string.Empty,
                Name = entity.Name ?? string.Empty,
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,
                Type = entity.Railway ?? string.Empty,
                WikipediaLink = entity.WikipediaLink
            };

            _logger.LogInformation("Successfully retrieved station with Area ID: {AreaId}, Station ID: {StationId}", areaId, stationId);
            return stationDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve station with Area ID: {AreaId}, Station ID: {StationId}", areaId, stationId);
            throw new InvalidOperationException($"Failed to retrieve station with Area ID: {areaId}, Station ID: {stationId}", ex);
        }
    }
}
