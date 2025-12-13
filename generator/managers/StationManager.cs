using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using Generator.Types;
namespace Generator.Managers;

public static class StationManager
{
    public static async Task RetrieveAndStoreStationsAsync(string areaName, double latitude, double longitude,
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
  node[""railway""=""halt""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
  node[""railway""=""tram_stop""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
  node[""highway""=""bus_stop""][""trolleybus""=""yes""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
  rel[""route""=""trolleybus""](around:{radiusMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
);
out body;";

            // Call Overpass API
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var requestBody = new StringContent(overpassQuery, System.Text.Encoding.UTF8, "text/plain");
            Console.WriteLine("  🌐 Calling Overpass API request body...");
            Console.WriteLine(overpassQuery);
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
            var trolleybusStopCount = 0;

            if (developerMode)
            {
                logger?.LogInformation("Developer mode enabled: limiting to first 3 stations per stationType");
                Console.WriteLine($"🔧 Developer mode: limiting to first 3 stations per stationType");
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
                    StationTypes stationType = StationTypes.Undefined;
                    
                    if (element.TryGetProperty("tags", out var tags))
                    {
                        if (tags.TryGetProperty("name", out var nameProperty))
                        {
                            stationName = nameProperty.GetString();
                        }

                        /* railway types mapping:
                         * node[""railway""=""station""]                          -> railway station
                         * node[""railway""=""halt""]                             -> railway station halt
                         * node[""railway""=""tram_stop"]                         -> tram stop
                         * tutti gli altri                                        -> trolleybus stop
                         */
                        tags.TryGetProperty("railway", out var railwayPropertyOverpass);

                        if (railwayPropertyOverpass.ValueKind == JsonValueKind.Undefined)
                        {
                            stationType = StationTypes.TrolleybusStop;
                        }
                        else if (railwayPropertyOverpass.GetString() == "station")
                        {
                            stationType = StationTypes.Station;
                        }
                        else if (railwayPropertyOverpass.GetString() == "halt")
                        {
                            stationType = StationTypes.Station;
                        }
                        else if (railwayPropertyOverpass.GetString() == "tram_stop")
                        {
                            stationType = StationTypes.TramStop;
                        }
                        else
                        {
                            stationType = StationTypes.Undefined;
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
                        Console.WriteLine($"  ⚠️ Skipping unnamed station (ID: {stationId})");
                        stationsSkipped++;
                        continue;
                    }

                    // Apply developer mode limits
                    if (developerMode)
                    {
                        if (stationType == StationTypes.Station && railwayStationCount >= 3)
                        {
                            logger?.LogDebug("Skipping railway station {StationName} - developer limit reached (3)", stationName);
                            stationsSkipped++;
                            continue;
                        }
                        if (stationType == StationTypes.TramStop && tramStopCount >= 3)
                        {
                            logger?.LogDebug("Skipping tram stop {StationName} - developer limit reached (3)", stationName);
                            stationsSkipped++;
                            continue;
                        }
                        if (stationType == StationTypes.TrolleybusStop && trolleybusStopCount >= 3)
                        {
                            logger?.LogDebug("Skipping trolleybus stop {StationName} - developer limit reached (3)", stationName);
                            stationsSkipped++;
                            continue;
                        }
                    }

                    string stationPartitionKey = areaName.Replace("/", "-").ToLowerInvariant();

                    // Create station entity
                    var station = new StationEntity
                    {
                        PartitionKey = stationPartitionKey,
                        RowKey = stationId,
                        Name = stationName,
                        Latitude = stationLat,
                        Longitude = stationLon,
                        WikipediaLink = wikipediaLink,
                        Railway = stationType.ToStringValue()
                    };

                    // Store in Azure Table Storage
                    await stationTableClient.UpsertEntityAsync(station, TableUpdateMode.Replace);
                    stationsProcessed++;

                    // Generate and save isochrone data (unless skipped)
                    if (!noIsochrone)
                    {
                        await GenerateAndSaveIsochroneAsync(areaName, stationId, stationName, stationLat, stationLon, stationType, logger, configuration);
                    }
                    else
                    {
                        logger?.LogInformation("Skipping isochrone generation for station: {StationName} (--noisochrone flag)", stationName);
                        Console.WriteLine($"  {stationName,-30} {stationType.ToStringValue(),-10} ⏭️ Skipping isochrone generation (--noisochrone flag)");
                    }

                    // Update counters for developer mode
                    if (developerMode)
                    {
                        if (stationType == StationTypes.Station)
                            railwayStationCount++;
                        else if (stationType == StationTypes.TramStop)
                            tramStopCount++;
                        else if (stationType == StationTypes.TrolleybusStop)
                            trolleybusStopCount++;
                    }

                    logger?.LogDebug("Stored station: {Name} (ID: {Id}, Railway: {Railway}) at ({Lat}, {Lon})",
                        stationName, stationId, stationType.ToStringValue(), stationLat, stationLon);
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

            areaName = areaName.Replace ("/", "-");
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

    public static async Task GenerateAndSaveIsochroneAsync(string areaName, string stationId, string stationName, 
        double latitude, double longitude, StationTypes stationType, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Generating isochrone data for station: {StationName} (ID: {StationId})", stationName, stationId);
            Console.WriteLine($"  📍 Generating isochrone data for station: {stationName}");

            // Get MapBox API key
            var mapBoxKey = configuration?.GetSection("AppSettings")[ConfigurationKeys.MapBoxSubscriptionKey];
            if (string.IsNullOrWhiteSpace(mapBoxKey) || mapBoxKey.Contains("<") || mapBoxKey.Contains(">"))
            {
                logger?.LogWarning("MapBox API key not configured, skipping isochrone generation for station: {StationName}", stationName);
                Console.WriteLine($"    ⚠️ MapBox API key not configured, skipping isochrone generation");
                return;
            }

            // Get Azure Storage connection using Azure AD
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (blobServiceClient == null)
            {
                logger?.LogWarning("Failed to create Azure Blob Storage client, skipping isochrone generation for station: {StationName}", stationName);
                return;
            }

            // Create blob service client and container
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
                        var styledIsochroneJson = AddStylingToIsochrone(isochroneJson, duration, stationType, logger);

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

    public static async Task GenerateAndSaveStationTypeIsochroneAsync(string areaName, StationTypes stationType, int duration, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Generating station type isochrone for area: {AreaName}, station type: {StationType}, duration: {Duration}min", areaName, stationType, duration);
            Console.WriteLine($"🚉 Generating {duration}min {stationType.ToStringValue()} isochrones for area: {areaName}");

            // Get Azure Storage connection using Azure AD
            TableServiceClient? tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (tableServiceClient == null || blobServiceClient == null)
            {
                logger?.LogError("Failed to create Azure Storage clients");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Check if isochrone container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogWarning("Isochrone container does not exist for area: {AreaName}", areaName);
                Console.WriteLine($"  ⚠️ No isochrone container found, cannot generate station type isochrones");
                return;
            }

            // Query all stations for this area with the specified station type
            var stationsPartitionKey = areaName.Replace('/', '-').ToLowerInvariant();
            var stations = new List<StationEntity>();
            await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{stationsPartitionKey}' and Railway eq '{stationType.ToStringValue()}'"))
            {
                stations.Add(station);
            }

            if (!stations.Any())
            {
                logger?.LogInformation("No stations found for area: {AreaName} with station type: {StationType}", areaName, stationType);
                Console.WriteLine($"  ℹ️ No {stationType.ToStringValue()} stations found in area: {areaName}");
                return;
            }

            Console.WriteLine($"  📊 Found {stations.Count} {stationType.ToStringValue()} stations to process");

            // Import required namespaces for GeoJSON processing
            var geoJsonReader = new NetTopologySuite.IO.GeoJsonReader();
            var geoJsonWriter = new NetTopologySuite.IO.GeoJsonWriter();

            try
            {
                logger?.LogInformation("Processing {Duration}min isochrones for station type: {StationType}", duration, stationType);
                Console.WriteLine($"  📍 Processing {duration}min isochrones...");

                    var geometries = new List<NetTopologySuite.Geometries.Geometry>();

                    // Read all isochrones from each station of this type for the current duration
                    foreach (var station in stations)
                    {
                        var stationIsochronePath = $"{areaName.ToLowerInvariant()}/{station.RowKey}/{duration}min.json";
                        var stationBlobClient = containerClient.GetBlobClient(stationIsochronePath);

                        try
                        {
                            var response = await stationBlobClient.DownloadContentAsync();
                            var geoJsonContent = response.Value.Content.ToString();

                            // Parse GeoJSON using NetTopologySuite
                            var stationFeatureCollection = geoJsonReader.Read<NetTopologySuite.Features.FeatureCollection>(geoJsonContent);
                            
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

                            logger?.LogDebug("Successfully parsed isochrone file: {BlobPath}", stationIsochronePath);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to read isochrone file for station {StationId}: {BlobPath}", station.RowKey, stationIsochronePath);
                        }
                    }

                    if (!geometries.Any())
                    {
                        logger?.LogWarning("No valid geometries found for station type: {StationType}, duration: {Duration}min", stationType, duration);
                        Console.WriteLine($"    ⚠️ No valid geometries found for {duration}min");
                        return;
                    }

                    logger?.LogInformation("Processing {Count} geometries for union operation", geometries.Count);

                    // Merge all isochrones into one
                    NetTopologySuite.Geometries.Geometry unionGeometry;
                    if (geometries.Count == 1)
                    {
                        unionGeometry = geometries[0];
                    }
                    else
                    {
                        // Use CascadedPolygonUnion for better performance with many polygons
                        var unionOp = new NetTopologySuite.Operation.Union.CascadedPolygonUnion(geometries);
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
                    var resultFeature = new NetTopologySuite.Features.Feature(unionGeometry, new NetTopologySuite.Features.AttributesTable());
                    
                    // Add properties based on station type with appropriate styling
                    string fillColor, strokeColor;
                    if (stationType == StationTypes.Station)
                    {
                        // Train station - green
                        fillColor = "#22c55e";
                        strokeColor = "#22c55e";
                    }
                    else if (stationType == StationTypes.TramStop)
                    {
                        // Tram stop - yellow
                        fillColor = "#eab308";
                        strokeColor = "#eab308";
                    }
                    else if (stationType == StationTypes.TrolleybusStop)
                    {
                        // Trolleybus stop - blue
                        fillColor = "#3b82f6";
                        strokeColor = "#3b82f6";
                    }
                    else
                    {
                        // Default for unknown types
                        fillColor = "#6b7280";
                        strokeColor = "#6b7280";
                    }

                    resultFeature.Attributes.Add("fill", fillColor);
                    resultFeature.Attributes.Add("stroke", strokeColor);
                    resultFeature.Attributes.Add("fill-opacity", 0.2); // Slightly more visible than individual station isochrones
                    resultFeature.Attributes.Add("stroke-width", 2);
                    resultFeature.Attributes.Add("contour", duration);
                    resultFeature.Attributes.Add("metric", "time");
                    resultFeature.Attributes.Add("type", "station-type");
                    resultFeature.Attributes.Add("railway-type", stationType.ToStringValue());

                    var resultFeatureCollection = new NetTopologySuite.Features.FeatureCollection { resultFeature };

                    // Convert to GeoJSON string
                    var geoJsonResult = geoJsonWriter.Write(resultFeatureCollection);

                    // Save it in container {areaName.ToLowerInvariant()}/{stationType}-{duration}min.json
                    var stationTypeIsochronePath = $"{areaName.ToLowerInvariant()}/{stationType.ToStringValue()}-{duration}min.json";
                    var stationTypeBlobClient = containerClient.GetBlobClient(stationTypeIsochronePath);

                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(geoJsonResult));
                    await stationTypeBlobClient.UploadAsync(stream, overwrite: true);

                    logger?.LogInformation("Successfully saved station type isochrone to: {BlobPath}", stationTypeIsochronePath);
                    Console.WriteLine($"    ✓ Saved {duration}min {stationType.ToStringValue()} isochrone to: {stationTypeIsochronePath}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to generate {Duration}min isochrone for station type: {StationType}", duration, stationType);
                Console.WriteLine($"    ❌ Failed to generate {duration}min isochrone: {ex.Message}");
                throw;
            }

            Console.WriteLine($"  ✅ Completed {duration}min station type isochrone generation for {stationType.ToStringValue()} stations in area: {areaName}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to generate station type isochrones for area: {AreaName}, station type: {StationType}", areaName, stationType);
            Console.WriteLine($"❌ Failed to generate station type isochrones for area: {areaName}, station type: {stationType.ToStringValue()}");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }



    private static string AddStylingToIsochrone(string isochroneJson, int duration, StationTypes stationType, ILogger? logger)
    {
        try
        {
            // Parse the original GeoJSON
            var jsonDoc = JsonDocument.Parse(isochroneJson);
            
            // Determine colors based on railway type
            string fillColor, strokeColor;
            if (stationType == StationTypes.Station)
            {
                // Train station - green
                fillColor = "#22c55e";
                strokeColor = "#22c55e";
            }
            else if (stationType == StationTypes.TramStop)
            {
                // Tram stop - yellow
                fillColor = "#eab308";
                strokeColor = "#eab308";
            }
            else if (stationType == StationTypes.TrolleybusStop)
            {
                // Halt - blue
                fillColor = "#3b82f6";
                strokeColor = "#3b82f6";
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
                    writer.WriteString("railway-type", stationType.ToStringValue());
                    
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

    public static async Task DeleteAreaStationsAsync(string areaName, TableClient stationTableClient, ILogger? logger)
    {
        // WARNING: areaname has format country/areaid
        areaName = areaName.Replace("/", "-");
        
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

    public static async Task ListStationsAsync(string areaId, string? filter, ILogger? logger, IConfiguration? configuration)
    {
        // WARNING: areaname has format country/areaid
        areaId = areaId.Replace("/", "-");

        try
        {
            logger?.LogInformation("Listing stations for area: {AreaId} with filter: {Filter}", areaId, filter ?? "none");

            // Get Azure Storage connection using Azure AD
            TableServiceClient? tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (tableServiceClient == null || blobServiceClient == null)
            {
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var stationTableClient = tableServiceClient.GetTableClient("station");
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

    public static async Task GenerateStationIsochroneAsync(string areaIdinput, string stationId, bool isDeleteMode, int? deleteDuration, ILogger? logger, IConfiguration? configuration)
    {
        // WARNING: areaid has format country/areaid
        string country = areaIdinput.Split('/')[0];
        string areaIdOnly = areaIdinput.Split('/')[1];
        string areaId = areaIdinput.Replace("/", "-");

        try
        {
            logger?.LogInformation("Processing isochrones for station: {StationId} in area: {AreaId} - Delete mode: {IsDeleteMode}, Duration: {DeleteDuration}", 
                stationId, areaId, isDeleteMode, deleteDuration?.ToString() ?? "all");

            // Get Azure Storage connection using Azure AD
            TableServiceClient? tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (tableServiceClient == null || blobServiceClient == null)
            {
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Check if area exists
            AreaEntity? area = null;
            try
            {
                var areaResponse = await areaTableClient.GetEntityAsync<AreaEntity>(country.ToLowerInvariant(), areaIdOnly.ToLowerInvariant());
                area = areaResponse.Value;
                logger?.LogInformation("Found area: {Country} {AreaName} ({DisplayName})", country, area.Name, area.DisplayName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"❌ Area '{country}'/'{areaId}' not found");
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
                var stationType = StationTypeExtensions.FromString(station.Railway);

                // Generate isochrone data for the station
                await GenerateAndSaveIsochroneAsync(areaId, stationId, station.Name, station.Latitude, station.Longitude, stationType, logger, configuration);
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

    public static async Task DeleteStationIsochroneAsync(string areaId, string stationId, int deleteDuration, 
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

    public static async Task DeleteAllStationIsochronesAsync(string areaId, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Deleting all station isochrones for area: {AreaId}", areaId);
            Console.WriteLine($"🗑️  Deleting all station isochrones for area: {areaId}");

            // Get Azure Storage connection using Azure AD
            TableServiceClient? tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (tableServiceClient == null || blobServiceClient == null)
            {
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Check if area exists
            AreaEntity? area = null;
            string areaPartitionKey = areaId.Split('/')[0].ToLowerInvariant();
            string areaRowKey = areaId.Split('/')[1].ToLowerInvariant();
            try
            {
                var areaResponse = await areaTableClient.GetEntityAsync<AreaEntity>(areaPartitionKey, areaRowKey);
                area = areaResponse.Value;
                logger?.LogInformation("Found area: {AreaName} ({DisplayName})", area.Name, area.DisplayName);
                Console.WriteLine($"📍 Area: {area.DisplayName ?? area.Name}");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"❌ Area '{areaId}' not found");
                Environment.Exit(1);
                return;
            }

            // Check if isochrone container exists
            if (!await containerClient.ExistsAsync())
            {
                logger?.LogWarning("Isochrone container does not exist");
                Console.WriteLine("⚠️  Isochrone container does not exist - nothing to delete");
                return;
            }

            // Query all stations for this area
            var stationsPartitionKey = areaId.Replace('/', '-').ToLowerInvariant();
            var stations = new List<StationEntity>();
            await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{stationsPartitionKey}'"))
            {
                stations.Add(station);
            }

            if (!stations.Any())
            {
                Console.WriteLine($"ℹ️  No stations found for area: {areaId}");
                return;
            }

            Console.WriteLine($"📊 Found {stations.Count} stations to process");
            Console.WriteLine();

            // Delete isochrones for each station
            var successCount = 0;
            var failedStations = new List<string>();
            var currentStationIndex = 1;

            foreach (var station in stations.OrderBy(s => s.Name))
            {
                try
                {
                    Console.WriteLine($"[{currentStationIndex}/{stations.Count}] 🗑️  Deleting isochrones for: {station.Name}");
                    Console.WriteLine($"   Station ID: {station.RowKey}");

                    // Delete all isochrones for this station (all durations)
                    await DeleteStationIsochroneAsync(areaId, station.RowKey, 0, containerClient, logger); // 0 means delete all
                    
                    Console.WriteLine($"   ✅ Successfully deleted isochrones for: {station.Name}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to delete isochrones for station: {StationId} ({StationName}) in area: {AreaId}", 
                        station.RowKey, station.Name, areaId);
                    Console.WriteLine($"   ❌ Failed to delete isochrones for: {station.Name} - {ex.Message}");
                    failedStations.Add($"{station.Name} ({station.RowKey})");
                }

                Console.WriteLine(); // Add spacing between stations
                currentStationIndex++;
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== DELETION SUMMARY ===");
            Console.WriteLine($"Total stations processed: {stations.Count}");
            Console.WriteLine($"Successfully processed: {successCount}");
            Console.WriteLine($"Failed: {failedStations.Count}");

            if (failedStations.Any())
            {
                Console.WriteLine($"Failed stations:");
                foreach (var failedStation in failedStations)
                {
                    Console.WriteLine($"  - {failedStation}");
                }
            }

            if (successCount == stations.Count)
            {
                logger?.LogInformation("Successfully deleted isochrones for all {Count} stations in area: {AreaId}", successCount, areaId);
                Console.WriteLine($"🎉 Successfully deleted all station isochrones for area: {areaId}");
            }
            else if (successCount > 0)
            {
                logger?.LogWarning("Deleted isochrones for {SuccessCount} out of {TotalCount} stations in area: {AreaId}", 
                    successCount, stations.Count, areaId);
                Console.WriteLine($"⚠️  Deleted isochrones for {successCount} out of {stations.Count} stations in area: {areaId}");
            }
            else
            {
                logger?.LogError("Failed to delete isochrones for any stations in area: {AreaId}", areaId);
                Console.WriteLine($"❌ Failed to delete isochrones for any stations in area: {areaId}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete all station isochrones for area: {AreaId}", areaId);
            Console.WriteLine($"❌ Failed to delete station isochrones for area: {areaId}");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }

    public static async Task RegenerateAllStationIsochronesAsync(string areaId, ILogger? logger, IConfiguration? configuration)
    {
        try
        {
            logger?.LogInformation("Regenerating all station isochrones for area: {AreaId}", areaId);
            Console.WriteLine($"🔄 Regenerating all station isochrones for area: {areaId}");

            // Get Azure Storage connection using Azure AD
            TableServiceClient? tableServiceClient = AzureStorageHelper.CreateTableServiceClient(configuration);
            BlobServiceClient? blobServiceClient = AzureStorageHelper.CreateBlobServiceClient(configuration);
            if (tableServiceClient == null || blobServiceClient == null)
            {
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage and Blob Storage
            var areaTableClient = tableServiceClient.GetTableClient("area");
            var stationTableClient = tableServiceClient.GetTableClient("station");
            var containerClient = blobServiceClient.GetBlobContainerClient("isochrone");

            logger?.LogInformation("Connected to Azure Storage services");

            // Check if area exists
            AreaEntity? area = null;
            try
            {
                var areaResponse = await areaTableClient.GetEntityAsync<AreaEntity>("area", areaId.ToLowerInvariant());
                area = areaResponse.Value;
                logger?.LogInformation("Found area: {AreaName} ({DisplayName})", area.Name, area.DisplayName);
                Console.WriteLine($"📍 Area: {area.DisplayName ?? area.Name}");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"❌ Area '{areaId}' not found");
                Environment.Exit(1);
                return;
            }

            // Query all stations for this area
            var stations = new List<StationEntity>();
            await foreach (var station in stationTableClient.QueryAsync<StationEntity>(
                filter: $"PartitionKey eq '{areaId.ToLowerInvariant()}'"))
            {
                stations.Add(station);
            }

            if (!stations.Any())
            {
                Console.WriteLine($"❌ No stations found for area: {areaId}");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"📊 Found {stations.Count} stations to process");
            Console.WriteLine();

            // Process each station
            var successCount = 0;
            var failedStations = new List<string>();
            var currentStationIndex = 1;

            foreach (var station in stations.OrderBy(s => s.Name))
            {
                try
                {
                    Console.WriteLine($"[{currentStationIndex}/{stations.Count}] 🚉 Processing station: {station.Name}");
                    Console.WriteLine($"   Station ID: {station.RowKey}");
                    Console.WriteLine($"   Location: {station.Latitude}, {station.Longitude}");
                    Console.WriteLine($"   Type: {station.Railway}");

                    // Delete existing isochrones for this station (all durations)
                    Console.WriteLine($"   🗑️  Deleting existing isochrones...");
                    await DeleteStationIsochroneAsync(areaId, station.RowKey, 0, containerClient, logger); // 0 means delete all

                    // Generate new isochrones for this station
                    Console.WriteLine($"   🔄 Generating new isochrones...");
                    var stationType = StationTypeExtensions.FromString(station.Railway);
                    await GenerateAndSaveIsochroneAsync(areaId, station.RowKey, station.Name, station.Latitude, station.Longitude, stationType, logger, configuration);
                    
                    Console.WriteLine($"   ✅ Successfully regenerated isochrones for: {station.Name}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to regenerate isochrones for station: {StationId} ({StationName}) in area: {AreaId}", 
                        station.RowKey, station.Name, areaId);
                    Console.WriteLine($"   ❌ Failed to regenerate isochrones for: {station.Name} - {ex.Message}");
                    failedStations.Add($"{station.Name} ({station.RowKey})");
                }

                Console.WriteLine(); // Add spacing between stations
                currentStationIndex++;
            }

            // Generate area-wide isochrones after all station isochrones are regenerated
            if (successCount > 0)
            {
                Console.WriteLine($"🌍 Generating area-wide isochrones...");
                var durations = new[] { 5, 10, 15, 20, 30 };
                
                foreach (var duration in durations)
                {
                    try
                    {
                        await AreaManager.GenerateAreaIsochroneAsync(areaId, duration, logger, configuration);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Failed to generate area-wide isochrone for duration: {Duration}min", duration);
                        Console.WriteLine($"    ❌ Failed to generate {duration}min area-wide isochrone: {ex.Message}");
                    }
                }
                Console.WriteLine($"✅ Area-wide isochrones generation completed");
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== REGENERATION SUMMARY ===");
            Console.WriteLine($"Total stations processed: {stations.Count}");
            Console.WriteLine($"Successfully regenerated: {successCount}");
            Console.WriteLine($"Failed: {failedStations.Count}");

            if (failedStations.Any())
            {
                Console.WriteLine($"Failed stations:");
                foreach (var failedStation in failedStations)
                {
                    Console.WriteLine($"  - {failedStation}");
                }
            }

            if (successCount == stations.Count)
            {
                logger?.LogInformation("Successfully regenerated isochrones for all {Count} stations in area: {AreaId}", successCount, areaId);
                Console.WriteLine($"🎉 Successfully regenerated isochrones for all stations in area: {areaId}");
            }
            else if (successCount > 0)
            {
                logger?.LogWarning("Regenerated isochrones for {SuccessCount} out of {TotalCount} stations in area: {AreaId}", 
                    successCount, stations.Count, areaId);
                Console.WriteLine($"⚠️  Regenerated isochrones for {successCount} out of {stations.Count} stations in area: {areaId}");
            }
            else
            {
                logger?.LogError("Failed to regenerate isochrones for any stations in area: {AreaId}", areaId);
                Console.WriteLine($"❌ Failed to regenerate isochrones for any stations in area: {areaId}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to regenerate all station isochrones for area: {AreaId}", areaId);
            Console.WriteLine($"❌ Failed to regenerate station isochrones for area: {areaId}");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }

    }
