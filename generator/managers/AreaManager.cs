using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using Generator.Types;
using Azure.Storage.Blobs;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Features;

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

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var tableServiceClient = new TableServiceClient(connectionString);
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");

            // Create tables if they don't exist
            await areaTableClient.CreateIfNotExistsAsync();
            await stationTableClient.CreateIfNotExistsAsync();
            logger?.LogInformation("Connected to Azure Table Storage");

            // Check if area already exists and clean up isochrone data if it does (only if we're going to generate new ones)
            if (!noIsochrone)
            {
                await CleanupExistingAreaIsochroneAsync(name, connectionString, logger);
            }

            // Create or update area entity
            var area = new AreaEntity
            {
                RowKey = name.ToLowerInvariant(),
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
            await RetrieveAndStoreStationsAsync(name, latitude, longitude, diameter, developerMode, noIsochrone, stationTableClient, logger, configuration);

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

    private static async Task RetrieveAndStoreStationsAsync(string areaName, double latitude, double longitude,
        int diameterMeters, bool developerMode, bool noIsochrone, TableClient stationTableClient, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Retrieving railway stations from Overpass API for area: {AreaName}", areaName);
            Console.WriteLine($"🔍 Retrieving railway stations within {diameterMeters}m of {latitude}, {longitude}...");

            // First, remove all existing stations for this area
            await RemoveExistingStationsAsync(areaName, stationTableClient, logger);

            // Calculate radius (diameter / 2)
            var radiusMeters = diameterMeters / 2;

            // Build Overpass QL query
            var overpassQuery = $@"[out:json][timeout:25];
(
  node[""railway""=""station""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
  node[""railway""=""tram_stop""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
);
out body;";

            // Call Overpass API
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var requestBody = new StringContent(overpassQuery, System.Text.Encoding.UTF8, "text/plain");
            var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogError("Overpass API request failed with status: {StatusCode}", response.StatusCode);
                Console.WriteLine($"❌ Failed to retrieve stations from Overpass API (Status: {response.StatusCode})");
                return;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            logger?.LogDebug("Overpass API response: {Response}", jsonContent);

            // Parse JSON response
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var elements = jsonDoc.RootElement.GetProperty("elements");

            var stationsProcessed = 0;
            var stationsSkipped = 0;
            var railwayStationCount = 0;
            var tramStopCount = 0;

            if (developerMode)
            {
                logger?.LogInformation("Developer mode enabled: limiting to first 3 railway stations and 3 tram stops");
                Console.WriteLine($"🔧 Developer mode: limiting to first 3 railway stations and 3 tram stops");
            }

            foreach (var element in elements.EnumerateArray())
            {
                try
                {
                    var stationId = element.GetProperty("id").GetInt64().ToString();
                    var stationLat = element.GetProperty("lat").GetDouble();
                    var stationLon = element.GetProperty("lon").GetDouble();

                    // Extract station name from tags
                    string? stationName = null;
                    string? wikipediaLink = null;
                    string? railwayType = null;

                    if (element.TryGetProperty("tags", out var tags))
                    {
                        if (tags.TryGetProperty("name", out var nameProperty))
                        {
                            stationName = nameProperty.GetString();
                        }

                        // Extract railway type
                        if (tags.TryGetProperty("railway", out var railwayProperty))
                        {
                            railwayType = railwayProperty.GetString();
                        }

                        // Try different Wikipedia tag formats
                        if (tags.TryGetProperty("wikipedia", out var wikiProperty))
                        {
                            var wikiValue = wikiProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(wikiValue) && wikiValue.Contains(':'))
                            {
                                // Format: xx:ZZZZZ -> https://xx.wikipedia.org/wiki/ZZZZZ
                                var parts = wikiValue.Split(':', 2);
                                if (parts.Length == 2)
                                {
                                    var languageCode = parts[0].Trim();
                                    var articleTitle = parts[1].Trim();
                                    wikipediaLink = $"https://{languageCode}.wikipedia.org/wiki/{articleTitle}";
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(wikiValue))
                            {
                                // If it's already a full URL, use as-is
                                wikipediaLink = wikiValue;
                            }
                        }
                    }

                    // Skip stations without names
                    if (string.IsNullOrWhiteSpace(stationName))
                    {
                        logger?.LogDebug("Skipping station {StationId} - no name found", stationId);
                        stationsSkipped++;
                        continue;
                    }

                    // Apply developer mode limits
                    if (developerMode)
                    {
                        if (railwayType == "station" && railwayStationCount >= 3)
                        {
                            logger?.LogDebug("Skipping railway station {StationName} - developer limit reached (3)", stationName);
                            stationsSkipped++;
                            continue;
                        }
                        if (railwayType == "tram_stop" && tramStopCount >= 3)
                        {
                            logger?.LogDebug("Skipping tram stop {StationName} - developer limit reached (3)", stationName);
                            stationsSkipped++;
                            continue;
                        }
                    }

                    // Create station entity
                    var station = new StationEntity
                    {
                        PartitionKey = areaName.ToLowerInvariant(),
                        RowKey = stationId,
                        Name = stationName,
                        Latitude = stationLat,
                        Longitude = stationLon,
                        WikipediaLink = wikipediaLink,
                        Railway = railwayType ?? "unknown"
                    };

                    // Store in Azure Table Storage
                    await stationTableClient.UpsertEntityAsync(station, TableUpdateMode.Replace);
                    stationsProcessed++;

                    // Generate and save isochrone data (unless skipped)
                    if (!noIsochrone)
                    {
                        await GenerateAndSaveIsochroneAsync(areaName, stationId, stationName, stationLat, stationLon, railwayType, logger, configuration);
                    }
                    else
                    {
                        logger?.LogInformation("Skipping isochrone generation for station: {StationName} (--noisochrone flag)", stationName);
                        Console.WriteLine($"  {stationName,-30} ⏭️ Skipping isochrone generation (--noisochrone flag)");
                    }

                    // Update counters for developer mode
                    if (developerMode)
                    {
                        if (railwayType == "station")
                            railwayStationCount++;
                        else if (railwayType == "tram_stop")
                            tramStopCount++;
                    }

                    logger?.LogDebug("Stored station: {Name} (ID: {Id}, Railway: {Railway}) at ({Lat}, {Lon})",
                        stationName, stationId, railwayType ?? "unknown", stationLat, stationLon);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to process station from Overpass API response");
                    stationsSkipped++;
                }
            }

            logger?.LogInformation("Station retrieval completed for area {AreaName}: {Processed} processed, {Skipped} skipped",
                areaName, stationsProcessed, stationsSkipped);

            if (noIsochrone)
            {
                Console.WriteLine($"✓ Retrieved and stored {stationsProcessed} stations (isochrone generation skipped)");
            }
            else
            {
                Console.WriteLine($"✓ Retrieved and stored {stationsProcessed} stations with isochrone data");
            }
            if (developerMode)
            {
                Console.WriteLine($"  🔧 Developer mode: limited to {railwayStationCount} railway stations and {tramStopCount} tram stops");
            }
            if (stationsSkipped > 0)
            {
                Console.WriteLine($"  ({stationsSkipped} stations skipped due to missing data or limits)");
            }
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError(ex, "Failed to connect to Overpass API");
            Console.WriteLine($"❌ Failed to retrieve stations from Overpass API: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            logger?.LogError(ex, "Overpass API request timed out");
            Console.WriteLine("❌ Overpass API request timed out");
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to parse Overpass API response");
            Console.WriteLine($"❌ Failed to parse Overpass API response: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error while retrieving stations");
            Console.WriteLine($"❌ Unexpected error while retrieving stations: {ex.Message}");
        }
    }

    private static async Task RemoveExistingStationsAsync(string areaName, TableClient stationTableClient, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Removing existing stations for area: {AreaName}", areaName);
            Console.WriteLine($"🗑️ Removing existing stations for area '{areaName}'...");

            var areaNameLower = areaName.ToLowerInvariant();
            var stationsToDelete = new List<StationEntity>();

            // Query all stations for this area (partition key = area name)
            await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{areaNameLower}'"))
            {
                stationsToDelete.Add(station);
            }

            // Delete stations in batches if any exist
            if (stationsToDelete.Count > 0)
            {
                logger?.LogInformation("Found {Count} existing stations to remove for area: {AreaName}",
                    stationsToDelete.Count, areaName);

                foreach (var station in stationsToDelete)
                {
                    try
                    {
                        await stationTableClient.DeleteEntityAsync(station.PartitionKey, station.RowKey);
                        logger?.LogDebug("Deleted station: {Name} (ID: {Id})", station.Name, station.RowKey);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete station {Name} (ID: {Id})", station.Name, station.RowKey);
                    }
                }

                Console.WriteLine($"✓ Removed {stationsToDelete.Count} existing stations");
            }
            else
            {
                logger?.LogInformation("No existing stations found for area: {AreaName}", areaName);
                Console.WriteLine($"ℹ️ No existing stations found for area '{areaName}'");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to remove existing stations for area: {AreaName}", areaName);
            Console.WriteLine($"❌ Failed to remove existing stations for area '{areaName}': {ex.Message}");
            // Don't throw here - we want to continue with adding new stations even if cleanup fails
        }
    }

    private static async Task GenerateAndSaveIsochroneAsync(string areaName, string stationId, string stationName, 
        double latitude, double longitude, string? railwayType, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Generating isochrone data for station: {StationName} (ID: {StationId})", stationName, stationId);
            Console.WriteLine($"  📍 Generating isochrone data for station: {stationName}");

            // Get MapBox API key
            var mapBoxKey = configuration?.GetSection("AppSettings")["mapBoxSubscriptionKey"];
            if (string.IsNullOrWhiteSpace(mapBoxKey) || mapBoxKey.Contains("<") || mapBoxKey.Contains(">"))
            {
                logger?.LogWarning("MapBox API key not configured, skipping isochrone generation for station: {StationName}", stationName);
                Console.WriteLine($"    ⚠️ MapBox API key not configured, skipping isochrone generation");
                return;
            }

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger?.LogWarning("Azure Storage connection string not configured, skipping isochrone generation for station: {StationName}", stationName);
                return;
            }

            // Create blob service client and container
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");
            await containerClient.CreateIfNotExistsAsync();

            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Generate isochrones for 5 and 10 minutes
            var durations = new[] { 5, 10, 15, 20, 30 };

            foreach (var duration in durations)
            {
                try
                {
                    // Build MapBox Isochrone API URL
                    var url = $"https://api.mapbox.com/isochrone/v1/mapbox/walking/{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}?contours_minutes={duration}&polygons=true&access_token={mapBoxKey}";

                    logger?.LogDebug("Calling MapBox Isochrone API for {Duration}min: {Url}", duration, url.Replace(mapBoxKey, "***"));

                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var isochroneJson = await response.Content.ReadAsStringAsync();
                        
                        // Validate JSON response
                        var jsonDoc = JsonDocument.Parse(isochroneJson);
                        if (!jsonDoc.RootElement.TryGetProperty("features", out _))
                        {
                            logger?.LogWarning("Invalid isochrone response for station {StationName} ({Duration}min): no features found", stationName, duration);
                            continue;
                        }

                        // Add styling properties to the GeoJSON
                        var styledIsochroneJson = AddStylingToIsochrone(isochroneJson, duration, railwayType, logger);

                        // Create blob path: /areaid/stationid/duration.json
                        var blobPath = $"{areaName.ToLowerInvariant()}/{stationId}/{duration}min.json";
                        var blobClient = containerClient.GetBlobClient(blobPath);

                        // Upload to blob storage
                        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(styledIsochroneJson));
                        await blobClient.UploadAsync(stream, overwrite: true);

                        logger?.LogInformation("Saved {Duration}min isochrone for station {StationName} to blob: {BlobPath}", 
                            duration, stationName, blobPath);
                        Console.WriteLine($"    ✓ Saved {duration}min isochrone to: {blobPath}");
                    }
                    else
                    {
                        logger?.LogError("MapBox Isochrone API request failed for station {StationName} ({Duration}min) with status: {StatusCode}", 
                            stationName, duration, response.StatusCode);
                        Console.WriteLine($"    ❌ Failed to get {duration}min isochrone (Status: {response.StatusCode})");
                        
                        var errorContent = await response.Content.ReadAsStringAsync();
                        logger?.LogDebug("MapBox API error response: {Response}", errorContent);
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger?.LogError(ex, "Failed to connect to MapBox Isochrone API for station {StationName} ({Duration}min)", stationName, duration);
                }
                catch (TaskCanceledException ex)
                {
                    logger?.LogError(ex, "MapBox Isochrone API request timed out for station {StationName} ({Duration}min)", stationName, duration);
                }
                catch (JsonException ex)
                {
                    logger?.LogError(ex, "Failed to parse MapBox Isochrone API response for station {StationName} ({Duration}min)", stationName, duration);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Unexpected error generating {Duration}min isochrone for station {StationName}", duration, stationName);
                }

                // Add a small delay between API calls to avoid rate limiting
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to generate isochrone data for station: {StationName} (ID: {StationId})", stationName, stationId);
            // Don't throw here - we want to continue processing other stations even if one fails
        }
    }

    private static string AddStylingToIsochrone(string isochroneJson, int duration, string? railwayType, ILogger? logger)
    {
        try
        {
            // Parse the original GeoJSON
            var jsonDoc = JsonDocument.Parse(isochroneJson);
            
            // Determine colors based on railway type
            string fillColor, strokeColor;
            if (railwayType == "station")
            {
                // Train station - green
                fillColor = "#22c55e";
                strokeColor = "#22c55e";
            }
            else if (railwayType == "tram_stop")
            {
                // Tram stop - yellow
                fillColor = "#eab308";
                strokeColor = "#eab308";
            }
            else
            {
                // Default for unknown types
                fillColor = "#6b7280";
                strokeColor = "#6b7280";
            }

            // Determine stroke properties based on duration
            var strokeWidth = duration == 30 ? 2 : 0;
            var fillOpacity = 0.1; // 10% transparency

            // Create a new JSON structure with styling
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            
            writer.WriteStartArray("features");
            
            // Process each feature in the original response
            if (jsonDoc.RootElement.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "Feature");
                    
                    // Copy geometry
                    if (feature.TryGetProperty("geometry", out var geometry))
                    {
                        writer.WritePropertyName("geometry");
                        geometry.WriteTo(writer);
                    }
                    
                    // Create properties with original data plus styling
                    writer.WriteStartObject("properties");
                    
                    // Copy original properties
                    if (feature.TryGetProperty("properties", out var originalProps))
                    {
                        foreach (var prop in originalProps.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            prop.Value.WriteTo(writer);
                        }
                    }
                    
                    // Add styling properties
                    writer.WriteString("fill", fillColor);
                    writer.WriteString("stroke", strokeColor);
                    writer.WriteNumber("fill-opacity", fillOpacity);
                    writer.WriteNumber("stroke-width", strokeWidth);
                    writer.WriteString("railway-type", railwayType ?? "unknown");
                    
                    writer.WriteEndObject(); // properties
                    writer.WriteEndObject(); // feature
                }
            }
            
            writer.WriteEndArray(); // features
            writer.WriteEndObject(); // root

            writer.Flush();
            var styledJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            
            logger?.LogDebug("Added styling to isochrone: fill={FillColor}, stroke={StrokeColor}, opacity={Opacity}, width={Width}", 
                fillColor, strokeColor, fillOpacity, strokeWidth);
                
            return styledJson;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to add styling to isochrone, returning original JSON");
            return isochroneJson;
        }
    }

    private static async Task CleanupExistingAreaIsochroneAsync(string areaName, string connectionString, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Checking for existing isochrone data for area: {AreaName}", areaName);

            // Create blob service client and container
            var blobServiceClient = new BlobServiceClient(connectionString);
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

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var tableServiceClient = new TableServiceClient(connectionString);
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

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var tableServiceClient = new TableServiceClient(connectionString);
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
            await DeleteAreaStationsAsync(name, stationTableClient, logger);

            // Step 2: Delete all isochrone data for this area
            await DeleteAreaIsochroneDataAsync(name, connectionString, logger);

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

    private static async Task DeleteAreaIsochroneDataAsync(string areaName, string connectionString, ILogger? logger)
    {
        try
        {
            logger?.LogInformation("Deleting all isochrone data for area: {AreaName}", areaName);
            Console.WriteLine($"  🗑️ Deleting isochrone data for area '{areaName}'...");

            // Create blob service client and container
            var blobServiceClient = new BlobServiceClient(connectionString);
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

    public static async Task ListStationsAsync(string areaId, string? filter, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Listing stations for area: {AreaId} with filter: {Filter}", areaId, filter ?? "none");

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var tableServiceClient = new TableServiceClient(connectionString);
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Query all stations for this area (partition key = area ID)
            var stations = new List<StationEntity>();
            await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{areaId.ToLowerInvariant()}'"))
            {
                stations.Add(station);
            }

            if (!stations.Any())
            {
                Console.WriteLine($"❌ No stations found for area: {areaId}");
                return;
            }

            // Apply filter if specified
            var filteredStations = stations;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                filteredStations = stations.Where(s => 
                    s.RowKey.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

                if (!filteredStations.Any())
                {
                    Console.WriteLine($"❌ No stations found for area: {areaId} with filter: {filter}");
                    return;
                }
            }

            Console.WriteLine($"Name: {areaId}");
            if (!string.IsNullOrWhiteSpace(filter))
            {
                Console.WriteLine($"Filter: {filter}");
            }
            Console.WriteLine("RowKey, Name, RailwayType, WikipediaLink, ABCDE");
            Console.WriteLine(new string('-', 80));

            // Check isochrone availability for each station
            var durations = new[] { 5, 10, 15, 20, 30 };

            foreach (var station in filteredStations.OrderBy(s => s.Name))
            {
                var isochroneStatus = "";
                
                // Check each duration (5, 10, 15, 20, 30 minutes)
                foreach (var duration in durations)
                {
                    var blobPath = $"{areaId.ToLowerInvariant()}/{station.RowKey}/{duration}min.json";
                    var blobClient = containerClient.GetBlobClient(blobPath);

                    try
                    {
                        var exists = await blobClient.ExistsAsync();
                        isochroneStatus += exists.Value ? "*" : "-";
                    }
                    catch
                    {
                        // If there's an error checking the blob, assume it doesn't exist
                        isochroneStatus += "-";
                    }
                }

                // Format the output line
                var wikipediaLink = !string.IsNullOrEmpty(station.WikipediaLink) ? station.WikipediaLink : "-";
                Console.WriteLine($"{station.RowKey}, {station.Name}, {station.Railway}, {wikipediaLink}, {isochroneStatus}");
            }

            Console.WriteLine();
            var totalStations = stations.Count;
            var displayedStations = filteredStations.Count;
            
            if (!string.IsNullOrWhiteSpace(filter))
            {
                Console.WriteLine($"✓ Listed {displayedStations} of {totalStations} stations for area '{areaId}' (filtered by '{filter}')");
            }
            else
            {
                Console.WriteLine($"✓ Listed {displayedStations} stations for area '{areaId}'");
            }
            Console.WriteLine("Legend: A=5min, B=10min, C=15min, D=20min, E=30min (* = available, - = not available)");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to list stations for area: {AreaId}", areaId);
            Console.WriteLine($"❌ Failed to list stations for area '{areaId}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task GenerateStationIsochroneAsync(string areaId, string stationId, bool isDeleteMode, int? deleteDuration, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Processing isochrones for station: {StationId} in area: {AreaId} - Delete mode: {IsDeleteMode}, Duration: {DeleteDuration}", 
                stationId, areaId, isDeleteMode, deleteDuration?.ToString() ?? "all");

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var tableServiceClient = new TableServiceClient(connectionString);
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Check if area exists
            AreaEntity? area = null;
            try
            {
                var areaResponse = await areaTableClient.GetEntityAsync<AreaEntity>("area", areaId.ToLowerInvariant());
                area = areaResponse.Value;
                logger?.LogInformation("Found area: {AreaName} ({DisplayName})", area.Name, area.DisplayName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"❌ Area '{areaId}' not found");
                Environment.Exit(1);
                return;
            }

            // Check if station exists in the specified area
            StationEntity? station = null;
            try
            {
                var stationResponse = await stationTableClient.GetEntityAsync<StationEntity>(areaId.ToLowerInvariant(), stationId);
                station = stationResponse.Value;
                logger?.LogInformation("Found station: {StationName} ({Railway}) at ({Lat}, {Lon})", 
                    station.Name, station.Railway, station.Latitude, station.Longitude);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"❌ Station '{stationId}' not found in area '{areaId}'");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🚉 Processing isochrone data for station: {station.Name}");
            Console.WriteLine($"   Area: {area.DisplayName ?? area.Name}");
            Console.WriteLine($"   Station ID: {stationId}");
            Console.WriteLine($"   Location: {station.Latitude}, {station.Longitude}");
            Console.WriteLine($"   Type: {station.Railway}");

            // Handle delete operation if specified
            if (isDeleteMode)
            {
                await DeleteStationIsochroneAsync(areaId, stationId, deleteDuration ?? 0, containerClient, logger);
            }
            else
            {
                // Generate isochrone data for the station
                await GenerateAndSaveIsochroneAsync(areaId, stationId, station.Name, station.Latitude, station.Longitude, station.Railway, logger, configuration);
                Console.WriteLine($"✓ Successfully generated isochrone data for station '{station.Name}'");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to process isochrones for station: {StationId} in area: {AreaId}", stationId, areaId);
            Console.WriteLine($"❌ Failed to process isochrones for station '{stationId}' in area '{areaId}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task DeleteStationIsochroneAsync(string areaId, string stationId, int deleteDuration, 
        BlobContainerClient containerClient, ILogger? logger)
    {
        try
        {
            var validDurations = new[] { 5, 10, 15, 20, 30 };
            var durationsToDelete = new List<int>();

            if (deleteDuration == 0)
            {
                // Delete all isochrones
                durationsToDelete.AddRange(validDurations);
                logger?.LogInformation("Deleting all isochrones for station: {StationId} in area: {AreaId}", stationId, areaId);
                Console.WriteLine($"🗑️ Deleting all isochrone data for station...");
            }
            else if (validDurations.Contains(deleteDuration))
            {
                // Delete specific duration
                durationsToDelete.Add(deleteDuration);
                logger?.LogInformation("Deleting {Duration}min isochrone for station: {StationId} in area: {AreaId}", 
                    deleteDuration, stationId, areaId);
                Console.WriteLine($"🗑️ Deleting {deleteDuration}min isochrone data for station...");
            }
            else
            {
                Console.WriteLine($"❌ Invalid duration '{deleteDuration}'. Valid values are: 5, 10, 15, 20, 30 (or 0 for all)");
                Environment.Exit(1);
                return;
            }

            var deletedCount = 0;
            var totalToDelete = durationsToDelete.Count;

            foreach (var duration in durationsToDelete)
            {
                var blobPath = $"{areaId.ToLowerInvariant()}/{stationId}/{duration}min.json";
                var blobClient = containerClient.GetBlobClient(blobPath);

                try
                {
                    var deleteResponse = await blobClient.DeleteIfExistsAsync();
                    if (deleteResponse.Value)
                    {
                        deletedCount++;
                        logger?.LogInformation("Deleted isochrone blob: {BlobPath}", blobPath);
                        Console.WriteLine($"  ✓ Deleted {duration}min isochrone: {blobPath}");
                    }
                    else
                    {
                        logger?.LogWarning("Isochrone blob not found: {BlobPath}", blobPath);
                        Console.WriteLine($"  ℹ️ {duration}min isochrone not found: {blobPath}");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to delete isochrone blob: {BlobPath}", blobPath);
                    Console.WriteLine($"  ❌ Failed to delete {duration}min isochrone: {ex.Message}");
                }
            }

            if (deleteDuration == 0)
            {
                Console.WriteLine($"✓ Deleted {deletedCount} of {totalToDelete} isochrone files for station");
            }
            else
            {
                Console.WriteLine($"✓ Successfully processed {deleteDuration}min isochrone deletion");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete isochrones for station: {StationId} in area: {AreaId}", stationId, areaId);
            Console.WriteLine($"❌ Failed to delete isochrones: {ex.Message}");
            throw;
        }
    }

    public static async Task GenerateAreaIsochroneAsync(string areaId, int duration, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Generating area-wide isochrone for area: {AreaId}, duration: {Duration}min", areaId, duration);
            Console.WriteLine($"  📍 Generating {duration}min area-wide isochrone...");

            // Get Azure Storage connection
            var connectionString = configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger?.LogError("Azure Storage connection string not configured");
                Console.WriteLine("❌ Azure Storage connection string not configured");
                return;
            }

            // Create blob service client and container
            var blobServiceClient = new BlobServiceClient(connectionString);
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
}
