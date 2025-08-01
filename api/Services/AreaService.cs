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

            // Ensure table exists
            await _storageService.EnsureTableExistsAsync(_tableName);

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

            // Ensure table exists
            await _storageService.EnsureTableExistsAsync(_tableName);

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
}
