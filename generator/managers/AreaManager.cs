using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using Generator.Types;
using Azure.Storage.Blobs;

namespace Generator.Managers;

public static class AreaManager
{
    public static async Task CreateAreaAsync(string name, string center, int diameter, string displayName, bool developerMode,
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

            // Check if area already exists and clean up isochrone data if it does
            await CleanupExistingAreaIsochroneAsync(name, connectionString, logger);

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
            await RetrieveAndStoreStationsAsync(name, latitude, longitude, diameter, developerMode, stationTableClient, logger, configuration);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create area: {Name}", name);
            Console.WriteLine($"❌ Failed to create area '{name}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RetrieveAndStoreStationsAsync(string areaName, double latitude, double longitude,
        int diameterMeters, bool developerMode, TableClient stationTableClient, ILogger? logger, IConfiguration? configuration)
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

                    // Generate and save isochrone data
                    await GenerateAndSaveIsochroneAsync(areaName, stationId, stationName, stationLat, stationLon, railwayType, logger, configuration);

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

            Console.WriteLine($"✓ Retrieved and stored {stationsProcessed} stations with isochrone data");
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
}
