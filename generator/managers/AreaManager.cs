using Azure.Data.Tables;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using Generator.Types;
using Azure.Storage.Blobs;
using Azure.Identity;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Features;
using Generator.Managers;

namespace Generator.Managers;

public static class AreaManager
{
    public static async Task CreateAreaAsync(string name, string center, int diameter, string displayName, bool developerMode, bool noIsochrone,
        ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Creating area: {Name} with display name: {DisplayName}", name, displayName);

            // Parse center coordinates
            var coordinates = center.Split(',');
            if (coordinates.Length != 2 ||
                !double.TryParse(coordinates[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(coordinates[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                Console.WriteLine("❌ Invalid center format. Use: latitude,longitude (e.g., 41.9028,12.4964)");
                Environment.Exit(1);
                return;
            }

            logger?.LogInformation("Parsed coordinates: Latitude={Latitude}, Longitude={Longitude}", latitude, longitude);

            // Validate coordinates
            if (latitude < -90 || latitude > 90)
            {
                Console.WriteLine($"❌ Latitude must be between -90 and 90 degrees (got {latitude})");
                Environment.Exit(1);
                return;
            }

            if (longitude < -180 || longitude > 180)
            {
                Console.WriteLine($"❌ Longitude must be between -180 and 180 degrees (got {longitude})");
                Environment.Exit(1);
                return;
            }

            if (diameter <= 0)
            {
                Console.WriteLine("❌ Diameter must be a positive number");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Console.WriteLine("❌ Display name cannot be empty");
                Environment.Exit(1);
                return;
            }

            // Get Azure Storage connection using Azure AD
            TableServiceClient tableServiceClient;
            try
            {
                tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create Azure Table Storage client: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");

            // Create tables if they don't exist
            await areaTableClient.CreateIfNotExistsAsync();
            await stationTableClient.CreateIfNotExistsAsync();
            logger?.LogInformation("Connected to Azure Table Storage");

            // Check if area already exists and clean up isochrone data if it does (only if we're going to generate new ones)
            if (!noIsochrone)
            {
                await CleanupExistingAreaIsochroneAsync(name, configuration, logger);
            }

            // populate partition and row key from name 'partition/row'
            var partitionKey = name.Contains('-') ? name.Split('-')[0] : "area";
            var rowKey = name.Contains('-') ? name.Split('-')[1] : name;

            // Create or update area entity
            var area = new AreaEntity
            {
                PartitionKey = partitionKey.ToLowerInvariant(),
                RowKey = rowKey.ToLowerInvariant(),
                Name = name,
                DisplayName = displayName,
                Latitude = latitude,
                Longitude = longitude,
                DiameterMeters = diameter
            };

            // Use UpsertEntity to replace if exists
            await areaTableClient.UpsertEntityAsync(area, TableUpdateMode.Replace);

            logger?.LogInformation("Area created/updated successfully: {Name} ({DisplayName}) at ({Lat},{Lon}) with diameter {Diameter}m",
                name, displayName, latitude, longitude, diameter);

            Console.WriteLine($"✓ Area '{name}' created successfully!");
            Console.WriteLine($"  Display Name: {displayName}");
            Console.WriteLine($"  Center: {latitude}, {longitude}");
            Console.WriteLine($"  Diameter: {diameter} meters");

            // Retrieve and store railway stations using Overpass API
            await StationManager.RetrieveAndStoreStationsAsync(name, latitude, longitude, diameter, developerMode, noIsochrone, stationTableClient, logger, configuration);

            // Generate area-wide isochrones if station isochrones were generated
            if (!noIsochrone)
            {
                logger?.LogInformation("Generating area-wide isochrones for area: {Name}", name);
                Console.WriteLine($"🌍 Generating area-wide isochrones for area: {name}");
                
                var durations = new[] { 5, 10, 15, 20, 30 };
                foreach (var duration in durations)
                {
                    await GenerateAreaIsochroneAsync(name, duration, logger, configuration);
                }
                
                Console.WriteLine($"✓ Area-wide isochrones generated successfully for area: {name}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create area: {Name}", name);
            Console.WriteLine($"❌ Failed to create area '{name}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task CleanupExistingAreaIsochroneAsync(string areaName, IConfiguration? configuration, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Checking for existing isochrone data for area: {AreaName}", areaName);

            // Create blob service client and container using Azure AD
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Blob Storage client");
                Console.WriteLine($"⚠️ Failed to create Azure Blob Storage client: {ex.Message}");
                return;
            }

            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogInformation("Isochrone container does not exist, no cleanup needed");
                return;
            }

            var areaPrefix = $"{areaName.ToLowerInvariant()}/";
            var blobsToDelete = new List<string>();

            // List all blobs with the area prefix
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                blobsToDelete.Add(blobItem.Name);
            }

            if (blobsToDelete.Count > 0)
            {
                logger?.LogInformation("Found {Count} existing isochrone files for area {AreaName}, deleting...", 
                    blobsToDelete.Count, areaName);
                Console.WriteLine($"🗑️ Cleaning up {blobsToDelete.Count} existing isochrone files for area '{areaName}'...");

                var deletedCount = 0;
                foreach (var blobName in blobsToDelete)
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blobName);
                        await blobClient.DeleteIfExistsAsync();
                        deletedCount++;
                        logger?.LogDebug("Deleted isochrone blob: {BlobName}", blobName);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete isochrone blob: {BlobName}", blobName);
                    }
                }

                Console.WriteLine($"✓ Cleaned up {deletedCount} isochrone files");
                logger?.LogInformation("Successfully deleted {DeletedCount} of {TotalCount} isochrone files for area {AreaName}", 
                    deletedCount, blobsToDelete.Count, areaName);
            }
            else
            {
                logger?.LogInformation("No existing isochrone files found for area: {AreaName}", areaName);
                Console.WriteLine($"ℹ️ No existing isochrone files found for area '{areaName}'");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to cleanup existing isochrone data for area: {AreaName}", areaName);
            Console.WriteLine($"⚠️ Failed to cleanup existing isochrone data for area '{areaName}': {ex.Message}");
            // Don't throw here - we want to continue with area creation even if cleanup fails
        }
    }

    public static async Task ListAreasAsync(ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Listing all areas");

            // Get Azure Storage connection using Azure AD
            TableServiceClient tableServiceClient;
            try
            {
                tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create Azure Table Storage client: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");

            // Get all areas (table will be created if it doesn't exist, but we'll handle empty results)
            var areas = new List<AreaEntity>();
            try
            {
                await foreach (var area in areaTableClient.QueryAsync<AreaEntity>())
                {
                    areas.Add(area);
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Table doesn't exist
                logger?.LogInformation("Area table does not exist");
                Console.WriteLine("📝 No areas found (area table does not exist)");
                return;
            }

            if (!areas.Any())
            {
                logger?.LogInformation("No areas found in table");
                Console.WriteLine("📝 No areas found");
                return;
            }

            // Sort areas by name for consistent output
            areas = areas.OrderBy(a => a.Name).ToList();

            logger?.LogInformation("Found {AreaCount} areas", areas.Count);
            Console.WriteLine($"📍 Found {areas.Count} area(s):");
            Console.WriteLine();

            // Display each area with station count
            foreach (var area in areas)
            {
                var stationCount = 0;

                if (!string.IsNullOrWhiteSpace(area.Name))
                {
                    try
                    {
                        // Count stations for this area (stations have PartitionKey = area name in lowercase)
                        var areaNameLower = area.Name.ToLowerInvariant();
                        await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                            filter: $"PartitionKey eq '{areaNameLower}'"))
                        {
                            stationCount++;
                        }
                    }
                    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                    {
                        // Station table doesn't exist, keep count at 0
                        logger?.LogDebug("Station table does not exist, station count will be 0");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to count stations for area: {AreaName}", area.Name);
                        stationCount = -1; // Indicate error
                    }
                }

                // Format: <area-id> <area-name> <station number>
                var stationDisplay = stationCount >= 0 ? stationCount.ToString() : "error";
                Console.WriteLine($"{area.RowKey} {area.DisplayName ?? area.Name ?? "Unknown"} {stationDisplay}");

                logger?.LogDebug("Area: {AreaId} ({AreaName}) - {StationCount} stations", 
                    area.RowKey, area.DisplayName ?? area.Name, stationCount);
            }

            Console.WriteLine();
            logger?.LogInformation("Listed {AreaCount} areas successfully", areas.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to list areas");
            Console.WriteLine($"❌ Failed to list areas: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task DeleteAreaAsync(string name, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Deleting area: {Name}", name);

            // Get Azure Storage connection using Azure AD
            TableServiceClient tableServiceClient;
            try
            {
                tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create Azure Table Storage client: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");

            // Try to get the area entity first to check if it exists
            AreaEntity? existingArea = null;
            try
            {
                var response = await areaTableClient.GetEntityAsync<AreaEntity>("area", name.ToLowerInvariant());
                existingArea = response.Value;
                logger?.LogInformation("Found area to delete: {Name} ({DisplayName})", existingArea.Name, existingArea.DisplayName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                logger?.LogWarning("Area not found: {Name}", name);
                Console.WriteLine($"❌ Area '{name}' not found");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🗑️ Deleting area '{name}' and all related data...");

            // Step 1: Delete all stations for this area
            await StationManager.DeleteAreaStationsAsync(name, stationTableClient, logger);

            // Step 2: Delete all isochrone data for this area
            await DeleteAreaIsochroneDataAsync(name, configuration, logger);

            // Step 3: Delete the area entity itself
            try
            {
                await areaTableClient.DeleteEntityAsync("area", name.ToLowerInvariant());
                logger?.LogInformation("Deleted area entity: {Name}", name);
                Console.WriteLine($"✓ Deleted area entity '{name}'");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to delete area entity: {Name}", name);
                Console.WriteLine($"❌ Failed to delete area entity '{name}': {ex.Message}");
                throw; // Re-throw since this is a critical failure
            }

            logger?.LogInformation("Area deleted successfully: {Name}", name);
            Console.WriteLine($"✓ Area '{name}' and all related data deleted successfully!");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete area: {Name}", name);
            Console.WriteLine($"❌ Failed to delete area '{name}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task DeleteAreaStationsAsync(string areaName, TableClient stationTableClient, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Deleting all stations for area: {AreaName}", areaName);
            Console.WriteLine($"  🗑️ Deleting stations for area '{areaName}'...");

            var areaNameLower = areaName.ToLowerInvariant();
            var stationsToDelete = new List<StationEntity>();

            // Query all stations for this area (partition key = area name)
            try
            {
                await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                    filter: $"PartitionKey eq '{areaNameLower}'"))
                {
                    stationsToDelete.Add(station);
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Station table doesn't exist
                logger?.LogInformation("Station table does not exist, no stations to delete");
                Console.WriteLine($"  ℹ️ No station table found, no stations to delete");
                return;
            }

            // Delete stations if any exist
            if (stationsToDelete.Count > 0)
            {
                logger?.LogInformation("Found {Count} stations to delete for area: {AreaName}",
                    stationsToDelete.Count, areaName);

                var deletedCount = 0;
                foreach (var station in stationsToDelete)
                {
                    try
                    {
                        await stationTableClient.DeleteEntityAsync(station.PartitionKey, station.RowKey);
                        deletedCount++;
                        logger?.LogDebug("Deleted station: {Name} (ID: {Id})", station.Name, station.RowKey);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete station {Name} (ID: {Id})", station.Name, station.RowKey);
                    }
                }

                Console.WriteLine($"  ✓ Deleted {deletedCount} stations");
                logger?.LogInformation("Successfully deleted {DeletedCount} of {TotalCount} stations for area {AreaName}",
                    deletedCount, stationsToDelete.Count, areaName);
            }
            else
            {
                logger?.LogInformation("No stations found for area: {AreaName}", areaName);
                Console.WriteLine($"  ℹ️ No stations found for area '{areaName}'");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete stations for area: {AreaName}", areaName);
            Console.WriteLine($"  ❌ Failed to delete stations for area '{areaName}': {ex.Message}");
            // Don't throw here - we want to continue with other cleanup steps
        }
    }

    private static async Task DeleteAreaIsochroneDataAsync(string areaName, IConfiguration? configuration, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Deleting all isochrone data for area: {AreaName}", areaName);
            Console.WriteLine($"  🗑️ Deleting isochrone data for area '{areaName}'...");

            // Create blob service client and container using Azure AD
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Blob Storage client");
                Console.WriteLine($"  ❌ Failed to create Azure Blob Storage client: {ex.Message}");
                return;
            }

            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogInformation("Isochrone container does not exist, no isochrone data to delete");
                Console.WriteLine($"  ℹ️ No isochrone container found, no isochrone data to delete");
                return;
            }

            var areaPrefix = $"{areaName.ToLowerInvariant()}/";
            var blobsToDelete = new List<string>();

            // List all blobs with the area prefix
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                blobsToDelete.Add(blobItem.Name);
            }

            if (blobsToDelete.Count > 0)
            {
                logger?.LogInformation("Found {Count} isochrone files to delete for area {AreaName}",
                    blobsToDelete.Count, areaName);

                var deletedCount = 0;
                foreach (var blobName in blobsToDelete)
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blobName);
                        await blobClient.DeleteIfExistsAsync();
                        deletedCount++;
                        logger?.LogDebug("Deleted isochrone blob: {BlobName}", blobName);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete isochrone blob: {BlobName}", blobName);
                    }
                }

                Console.WriteLine($"  ✓ Deleted {deletedCount} isochrone files");
                logger?.LogInformation("Successfully deleted {DeletedCount} of {TotalCount} isochrone files for area {AreaName}",
                    deletedCount, blobsToDelete.Count, areaName);
            }
            else
            {
                logger?.LogInformation("No isochrone files found for area: {AreaName}", areaName);
                Console.WriteLine($"  ℹ️ No isochrone files found for area '{areaName}'");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete isochrone data for area: {AreaName}", areaName);
            Console.WriteLine($"  ❌ Failed to delete isochrone data for area '{areaName}': {ex.Message}");
            // Don't throw here - we want to continue with other cleanup steps
        }
    }

    public static async Task GenerateAreaIsochroneAsync(string areaId, int duration, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Generating area-wide isochrone for area: {AreaId}, duration: {Duration}min", areaId, duration);
            Console.WriteLine($"  📍 Generating {duration}min area-wide isochrone...");

            // Get Azure Storage connection using Azure AD
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Blob Storage client");
                Console.WriteLine($"❌ Failed to create Azure Blob Storage client: {ex.Message}");
                return;
            }

            // Create blob service client and container
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogWarning("Isochrone container does not exist for area: {AreaId}", areaId);
                Console.WriteLine($"    ⚠️ No isochrone container found, skipping area-wide isochrone generation");
                return;
            }

            // Read all station isochrone files for this duration
            var areaPrefix = $"{areaId.ToLowerInvariant()}/";
            var isochroneFiles = new List<string>();
            var geoJsonReader = new GeoJsonReader();
            var geometries = new List<Geometry>();

            // Find all station isochrone files for this duration
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                if (blobItem.Name.EndsWith($"/{duration}min.json") && 
                    blobItem.Name.Count(c => c == '/') == 2) // Ensure it's a station file, not an area file
                {
                    isochroneFiles.Add(blobItem.Name);
                }
            }

            if (!isochroneFiles.Any())
            {
                logger?.LogWarning("No station isochrone files found for area: {AreaId}, duration: {Duration}min", areaId, duration);
                Console.WriteLine($"    ⚠️ No station isochrone files found for {duration}min");
                return;
            }

            logger?.LogInformation("Found {Count} station isochrone files for area: {AreaId}, duration: {Duration}min", 
                isochroneFiles.Count, areaId, duration);

            // Read and parse each isochrone file
            foreach (var blobPath in isochroneFiles)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobPath);
                    var response = await blobClient.DownloadContentAsync();
                    var geoJsonContent = response.Value.Content.ToString();

                    // Parse GeoJSON using NetTopologySuite
                    var stationFeatureCollection = geoJsonReader.Read<FeatureCollection>(geoJsonContent);
                    
                    if (stationFeatureCollection != null)
                    {
                        foreach (var stationFeature in stationFeatureCollection)
                        {
                            if (stationFeature.Geometry != null)
                            {
                                geometries.Add(stationFeature.Geometry);
                            }
                        }
                    }

                    logger?.LogDebug("Successfully parsed isochrone file: {BlobPath}", blobPath);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to parse isochrone file: {BlobPath}", blobPath);
                }
            }

            if (!geometries.Any())
            {
                logger?.LogWarning("No valid geometries found in isochrone files for area: {AreaId}, duration: {Duration}min", areaId, duration);
                Console.WriteLine($"    ⚠️ No valid geometries found in isochrone files for {duration}min");
                return;
            }

            logger?.LogInformation("Processing {Count} geometries for union operation", geometries.Count);

            // Create union of all geometries
            Geometry unionGeometry;
            if (geometries.Count == 1)
            {
                unionGeometry = geometries[0];
            }
            else
            {
                // Use CascadedPolygonUnion for better performance with many polygons
                var unionOp = new CascadedPolygonUnion(geometries);
                unionGeometry = unionOp.Union();
            }

            // Ensure the result is valid
            if (!unionGeometry.IsValid)
            {
                logger?.LogWarning("Union geometry is not valid, attempting to buffer with 0 distance");
                unionGeometry = unionGeometry.Buffer(0);
            }

            logger?.LogInformation("Successfully created union geometry with {GeomType}", unionGeometry.GeometryType);

            // Create GeoJSON feature collection with the union geometry
            var resultFeature = new Feature(unionGeometry, new AttributesTable());
            
            // Add properties similar to station isochrones
            resultFeature.Attributes.Add("fill", "#3b82f6"); // Blue color for area-wide isochrone
            resultFeature.Attributes.Add("stroke", "#3b82f6");
            resultFeature.Attributes.Add("fill-opacity", 0.15); // Slightly more visible than station isochrones
            resultFeature.Attributes.Add("stroke-width", 2);
            resultFeature.Attributes.Add("contour", duration);
            resultFeature.Attributes.Add("metric", "time");
            resultFeature.Attributes.Add("type", "area-wide");

            var resultFeatureCollection = new FeatureCollection { resultFeature };

            // Convert to GeoJSON string
            var geoJsonWriter = new GeoJsonWriter();
            var geoJsonResult = geoJsonWriter.Write(resultFeatureCollection);

            // Save the result to blob storage at /isochrone/areaid/ddmin.json
            var areaIsochroneBlobPath = $"{areaId.ToLowerInvariant()}/{duration}min.json";
            var areaIsochroneBlobClient = containerClient.GetBlobClient(areaIsochroneBlobPath);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(geoJsonResult));
            await areaIsochroneBlobClient.UploadAsync(stream, overwrite: true);

            logger?.LogInformation("Successfully saved area-wide isochrone to: {BlobPath}", areaIsochroneBlobPath);
            Console.WriteLine($"    ✓ Saved {duration}min area-wide isochrone to: {areaIsochroneBlobPath}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to generate area-wide isochrone for area: {AreaId}, duration: {Duration}min", areaId, duration);
            Console.WriteLine($"    ❌ Failed to generate {duration}min area-wide isochrone: {ex.Message}");
            // Don't throw here - we want to continue with other durations even if one fails
        }
    }

    public static async Task DeleteAreaIsochronesAsync(string areaName, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Deleting area-wide isochrones for area: {AreaName}", areaName);
            Console.WriteLine($"🗑️  Deleting area-wide isochrones for area: {areaName}");

            // Get Azure Storage connection using Azure AD
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Blob Storage client");
                Console.WriteLine($"❌ Failed to create Azure Blob Storage client: {ex.Message}");
                return;
            }

            // Create blob service client and container
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogWarning("Isochrone container does not exist");
                Console.WriteLine("⚠️  Isochrone container does not exist - nothing to delete");
                return;
            }

            // Find area-wide isochrones to delete (files with only 2 path segments: area/duration.json)
            var areaPrefix = $"{areaName.ToLowerInvariant()}/";
            var areaIsochronesToDelete = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                if (blobItem.Name.EndsWith(".json") && blobItem.Name.Count(c => c == '/') == 1) // Only 1 slash means area/duration.json
                {
                    areaIsochronesToDelete.Add(blobItem.Name);
                }
            }

            if (!areaIsochronesToDelete.Any())
            {
                logger?.LogInformation("No area-wide isochrones found for area: {AreaName}", areaName);
                Console.WriteLine($"  ℹ️  No area-wide isochrones found for area: {areaName}");
                return;
            }

            logger?.LogInformation("Found {Count} area-wide isochrones to delete for area: {AreaName}", 
                areaIsochronesToDelete.Count, areaName);
            Console.WriteLine($"  📋 Found {areaIsochronesToDelete.Count} area-wide isochrones to delete:");

            // Delete area-wide isochrones
            var successCount = 0;
            foreach (var blobPath in areaIsochronesToDelete)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobPath);
                    await blobClient.DeleteIfExistsAsync();
                    
                    // Extract duration from filename for display
                    var fileName = System.IO.Path.GetFileName(blobPath);
                    logger?.LogDebug("Deleted area-wide isochrone: {BlobPath}", blobPath);
                    Console.WriteLine($"    ✓ Deleted: {fileName}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to delete area-wide isochrone: {BlobPath}", blobPath);
                    Console.WriteLine($"    ❌ Failed to delete: {System.IO.Path.GetFileName(blobPath)} - {ex.Message}");
                }
            }

            if (successCount == areaIsochronesToDelete.Count)
            {
                logger?.LogInformation("Successfully deleted all {Count} area-wide isochrones for area: {AreaName}", 
                    successCount, areaName);
                Console.WriteLine($"✅ Successfully deleted all {successCount} area-wide isochrones for area: {areaName}");
            }
            else if (successCount > 0)
            {
                logger?.LogWarning("Deleted {SuccessCount} out of {TotalCount} area-wide isochrones for area: {AreaName}", 
                    successCount, areaIsochronesToDelete.Count, areaName);
                Console.WriteLine($"⚠️  Deleted {successCount} out of {areaIsochronesToDelete.Count} area-wide isochrones for area: {areaName}");
            }
            else
            {
                logger?.LogError("Failed to delete any area-wide isochrones for area: {AreaName}", areaName);
                Console.WriteLine($"❌ Failed to delete any area-wide isochrones for area: {areaName}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete area-wide isochrones for area: {AreaName}", areaName);
            Console.WriteLine($"❌ Failed to delete area-wide isochrones for area: {areaName}");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }

    public static async Task RecreateAreaIsochronesAsync(string areaName, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Recreating area-wide isochrones for area: {AreaName}", areaName);
            Console.WriteLine($"🔄 Recreating area-wide isochrones for area: {areaName}");

            // Get Azure Storage connection using Azure AD
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Blob Storage client");
                Console.WriteLine($"❌ Failed to create Azure Blob Storage client: {ex.Message}");
                return;
            }

            // Create blob service client and container
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogError("Isochrone container does not exist");
                Console.WriteLine("❌ Isochrone container does not exist");
                return;
            }

            // Check if any station isochrones exist for this area
            var areaPrefix = $"{areaName.ToLowerInvariant()}/";
            var hasStationIsochrones = false;

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                // Look for station isochrone files (has 3 path segments: area/station/duration.json)
                if (blobItem.Name.EndsWith(".json") && blobItem.Name.Count(c => c == '/') == 2)
                {
                    hasStationIsochrones = true;
                    break;
                }
            }

            if (!hasStationIsochrones)
            {
                logger?.LogError("No station isochrones found for area: {AreaName}", areaName);
                Console.WriteLine($"❌ No station isochrones found for area: {areaName}");
                Console.WriteLine($"   Please create the area first using: area create {areaName} --center <lat,lon> --diameter <meters> --displayname \"<name>\"");
                return;
            }

            // Delete existing area-wide isochrones (files with only 2 path segments: area/duration.json)
            var areaIsochronesToDelete = new List<string>();
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: areaPrefix))
            {
                if (blobItem.Name.EndsWith(".json") && blobItem.Name.Count(c => c == '/') == 1) // Only 1 slash means area/duration.json
                {
                    areaIsochronesToDelete.Add(blobItem.Name);
                }
            }

            // Delete existing area-wide isochrones
            if (areaIsochronesToDelete.Any())
            {
                logger?.LogInformation("Deleting {Count} existing area-wide isochrones for area: {AreaName}", 
                    areaIsochronesToDelete.Count, areaName);
                Console.WriteLine($"  🗑️  Deleting {areaIsochronesToDelete.Count} existing area-wide isochrones...");

                foreach (var blobPath in areaIsochronesToDelete)
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blobPath);
                        await blobClient.DeleteIfExistsAsync();
                        logger?.LogDebug("Deleted area-wide isochrone: {BlobPath}", blobPath);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete area-wide isochrone: {BlobPath}", blobPath);
                    }
                }
            }

            // Generate area-wide isochrones for each duration (5, 10, 15, 20, 30)
            var durations = new[] { 5, 10, 15, 20, 30 };
            var successCount = 0;

            foreach (var duration in durations)
            {
                try
                {
                    await GenerateAreaIsochroneAsync(areaName, duration, logger, configuration);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to generate area-wide isochrone for duration: {Duration}min", duration);
                    Console.WriteLine($"    ❌ Failed to generate {duration}min isochrone: {ex.Message}");
                }
            }

            if (successCount == durations.Length)
            {
                logger?.LogInformation("Successfully recreated all area-wide isochrones for area: {AreaName}", areaName);
                Console.WriteLine($"✅ Successfully recreated all area-wide isochrones for area: {areaName}");
            }
            else if (successCount > 0)
            {
                logger?.LogWarning("Recreated {SuccessCount} out of {TotalCount} area-wide isochrones for area: {AreaName}", 
                    successCount, durations.Length, areaName);
                Console.WriteLine($"⚠️  Recreated {successCount} out of {durations.Length} area-wide isochrones for area: {areaName}");
            }
            else
            {
                logger?.LogError("Failed to recreate any area-wide isochrones for area: {AreaName}", areaName);
                Console.WriteLine($"❌ Failed to recreate any area-wide isochrones for area: {areaName}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to recreate area-wide isochrones for area: {AreaName}", areaName);
            Console.WriteLine($"❌ Failed to recreate area-wide isochrones for area: {areaName}");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }
}
