# Generator - Metro Proximity Generator

A .NET 8 console application for managing areas and generating station proximity data using Azure Maps or MapBox APIs.

## Features

- .NET 8 console application
- Area management with Azure Table Storage
- Configuration management using JSON files
- Azure Storage Blob and Table client integration
- MapBox API integration for geographical data and isochrone generation
- Automatic isochrone polygon generation for 5 and 10-minute walking distances
- Comprehensive logging with configurable levels
- Error handling with retry logic
- Security best practices
- Command-line interface with System.CommandLine

## Prerequisites

- .NET 8.0 SDK
- Azure Storage Account (for area storage, station data, and isochrone polygons)
- MapBox API Key (for geographical services and isochrone generation)

## Commands

### Area Management

#### Create Area
Create a new area with center coordinates and diameter:

```bash
dotnet run -- area create <name> --center <latitude,longitude> --diameter <meters> --displayname <display_name> [--developer] [--noisochrone]
```

**Parameters:**
- `<name>`: Name of the area (required)
- `--center`: Center coordinates as 'latitude,longitude' (required)
- `--diameter`: Diameter in meters (required)
- `--displayname`: Display name for the area (required)
- `--developer`: Developer mode - limit to first 3 railway stations and 3 tram stops (optional)
- `--noisochrone`: Skip isochrone generation when creating the area (optional)

**Isochrone Generation:**
When creating an area (unless `--noisochrone` is specified), the system will:
1. Generate individual isochrones for each station (5, 10, 15, 20, 30 minutes)
2. Create area-wide isochrones by computing the union of all station isochrones for each duration
3. Save area-wide isochrones to `/isochrone/{areaid}/{duration}min.json`

The area-wide isochrones represent the combined accessibility area from all transit stations in the area, handling overlaps correctly using geometric union operations.

**Examples:**
```bash
# Create an area centered in Rome with 1km diameter
dotnet run -- area create "rome-center" --center "41.9028,12.4964" --diameter 1000 --displayname "Rome City Center"

# Create an area at equator with 500m diameter in developer mode
dotnet run -- area create "equator-point" --center "0,0" --diameter 500 --displayname "Equator Reference Point" --developer

# Create an area with debug logging and developer mode
dotnet run -- area create "test" --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug

# Create an area without generating isochrones
dotnet run -- area create "stations-only" --center "41.9028,12.4964" --diameter 1000 --displayname "Stations Only" --noisochrone
```

#### Delete Area
Delete an existing area and all its related data:

```bash
dotnet run -- area delete <name>
```

**What gets deleted:**
- The area entity from Azure Table Storage
- All stations associated with the area
- All isochrone files for the area from Azure Blob Storage

**Parameters:**
- `<name>`: Name of the area to delete (required)

**Examples:**
```bash
# Delete the Rome area and all its data
dotnet run -- area delete "rome-center"

# Delete with detailed logging
dotnet run -- area delete "rome-center" --logging Information
```

**Sample Output:**
```bash
🗑️ Deleting area 'test-area' and all related data...
  🗑️ Deleting stations for area 'test-area'...
  ✓ Deleted 15 stations
  🗑️ Deleting isochrone data for area 'test-area'...
  ✓ Deleted 75 isochrone files
✓ Deleted area entity 'test-area'
✓ Area 'test-area' and all related data deleted successfully!
```

**Important Notes:**
- This operation is **irreversible** - all data associated with the area will be permanently deleted
- If the area doesn't exist, the command will display an error and exit
- Partial failures (e.g., unable to delete some isochrone files) will be logged but won't prevent the overall deletion

#### List Areas
List all existing areas with their station counts:

```bash
dotnet run -- area list
```

**Output Format:**
Each area is displayed on a single line with the format: `<area-id> <area-name> <station-count>`

**Examples:**
```bash
# List all areas
dotnet run -- area list

# List all areas with detailed logging
dotnet run -- area list --logging Information
```

**Sample Output:**
```bash
📍 Found 3 area(s):

milan Milano Dev 6
naples Napoli 146
rome Roma 321
```

**Output Details:**
- **area-id**: The unique identifier used internally (row key in storage)
- **area-name**: The display name of the area
- **station-count**: Number of stations stored for this area

If no areas exist, the command will display:
```bash
📝 No areas found
```

#### Area Cleanup Behavior

When creating an area that already exists, the application automatically:

1. **Cleans up existing isochrone data**: Deletes all isochrone files from Azure Blob Storage for the area
2. **Removes existing station data**: Clears all station records from Azure Table Storage
3. **Regenerates everything**: Creates fresh station and isochrone data

**Console Output for Existing Area:**
```bash
🗑️ Cleaning up 20 existing isochrone files for area 'rome-center'...
✓ Cleaned up 20 isochrone files
🗑️ Removing existing stations for area 'rome-center'...
✓ Removed 15 existing stations
🔍 Retrieving railway stations within 5000m...
✓ Retrieved and stored 15 stations with isochrone data
```

This ensures data consistency and prevents accumulation of outdated isochrone polygons.



### Station Management

#### List Stations
List all stations for a specific area:

```bash
dotnet run -- station list <areaid> [--filter <text>]
```

**Parameters:**
- `<areaid>`: The area ID to list stations for (required)
- `--filter <text>`: Filter stations by rowkey or name containing the specified text (optional)

**Output Format:**
```
Name: <areaid>
[Filter: <filter>]
RowKey, Name, RailwayType, WikipediaLink, ABCDE
```

Where ABCDE represents isochrone availability:
- A = 5-minute isochrone
- B = 10-minute isochrone  
- C = 15-minute isochrone
- D = 20-minute isochrone
- E = 30-minute isochrone
- `*` = Available, `-` = Not available

**Examples:**
```bash
# List all stations for Rome area
dotnet run -- station list rome

# Filter stations containing "Roma" in name or rowkey
dotnet run -- station list rome --filter Roma

# Filter stations containing "30" in rowkey
dotnet run -- station list milan --filter 30
```

#### Generate Station Isochrones
Generate or delete isochrone data for a specific station:

```bash
dotnet run -- station isochrone <areaid> <stationid> [--delete [duration]]
```

**Parameters:**
- `<areaid>`: The area ID containing the station (required)
- `<stationid>`: The station ID to process (required)
- `--delete [duration]`: Delete isochrone(s) instead of generating (optional)
  - Without value: Delete all isochrones (5, 10, 15, 20, 30 minutes)
  - With value: Delete specific duration (5, 10, 15, 20, or 30)

**Examples:**
```bash
# Generate all isochrones for a station
dotnet run -- station isochrone milan 21226369

# Delete all isochrones for a station
dotnet run -- station isochrone milan 21226369 --delete

# Delete only the 10-minute isochrone
dotnet run -- station isochrone milan 21226369 --delete 10

# Delete only the 30-minute isochrone
dotnet run -- station isochrone rome 354964363 --delete 30
```

**What gets generated/deleted:**
- 5-minute walking isochrone
- 10-minute walking isochrone
- 15-minute walking isochrone
- 20-minute walking isochrone
- 30-minute walking isochrone

**Error Handling:**
- Shows error if area doesn't exist
- Shows error if station doesn't exist in the specified area
- Shows error for invalid duration values
- Gracefully handles non-existent isochrone files during deletion

### Global Options

All commands support the following options:

- `-l, --logging <level>`: Set the minimum log level (Trace, Debug, Information, Warning, Error, Critical, None). Default: None
- `--developer`: Developer mode for create area command - limits station retrieval to first 3 railway stations and 3 tram stops

**Examples:**
```bash
# Create area with debug logging
dotnet run -- area create "test" --center "40,40" --diameter 1000 --displayname "Test Area" --logging Debug

# Create area in developer mode with limited stations
dotnet run -- area create "test" --center "40,40" --diameter 1000 --displayname "Test Area" --developer

# Combine developer mode with logging
dotnet run -- area create "test" --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug

# Generate data with warning-only logging
dotnet run -- generate --logging Warning
```

## Developer Mode

The `--developer` flag is designed to speed up development and testing by limiting the number of stations retrieved from the Overpass API when creating areas.

### How Developer Mode Works

When the `--developer` flag is used with the `area create` command:

1. **Station Limiting**: Only the first 3 railway stations and 3 tram stops are stored
2. **Faster Processing**: Reduces API response processing time and storage operations  
3. **Development Friendly**: Makes testing with smaller datasets more manageable
4. **Clear Indication**: Console output clearly shows when developer mode is active

### Developer Mode Output

```bash
# Normal mode output
✓ Retrieved and stored 45 stations with isochrone data

# Developer mode output  
🔧 Developer mode: limiting to first 3 railway stations and 3 tram stops
✓ Retrieved and stored 6 stations with isochrone data
  🔧 Developer mode: limited to 3 railway stations and 3 tram stops
```

### When to Use Developer Mode

- **Initial Development**: Testing area creation functionality
- **Configuration Testing**: Verifying Azure Storage and API connections
- **Debugging**: Investigating issues without processing large datasets
- **Rapid Iteration**: Quickly testing different coordinates and parameters

**Note**: Developer mode only affects the `area create` command. Other commands work normally.

## Isochrone Generation

The application automatically generates walking distance isochrone polygons for each station using the MapBox Isochrone API.

### How Isochrone Generation Works

When creating an area, the application:

1. **Retrieves Stations**: Gets railway stations and tram stops from OpenStreetMap via Overpass API
2. **Generates Isochrones**: For each station, calls MapBox Isochrone API to generate walking distance polygons
3. **Multiple Durations**: Creates isochrones for 5-minute and 10-minute walking distances
4. **Stores in Azure**: Saves the GeoJSON polygons to Azure Blob Storage in the "isochrone" container

### Isochrone File Structure

Isochrone files are stored in Azure Blob Storage using this naming convention:

```
Container: isochrone
Path Structure: /{area-id}/{station-id}/{duration}.json

Examples:
- /rome-center/354964363/5min.json    # 5-minute walking isochrone for station 354964363
- /rome-center/354964363/10min.json   # 10-minute walking isochrone for station 354964363
- /rome-center/5216453777/5min.json   # 5-minute walking isochrone for station 5216453777
- /rome-center/5216453777/10min.json  # 10-minute walking isochrone for station 5216453777
```

### Isochrone Data Format

Each isochrone file contains a GeoJSON FeatureCollection with polygon data and styling properties:

```json
{
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[12.495825, 41.9028729], ...]]
      },
      "properties": {
        "contour": 5,
        "metric": "time",
        "unit": "minutes",
        "fill": "#22c55e",
        "stroke": "#22c55e", 
        "fill-opacity": 0.1,
        "stroke-width": 0,
        "railway-type": "station"
      }
    }
  ],
  "type": "FeatureCollection"
}
```

### Isochrone Styling

The application automatically applies styling based on station type and duration:

#### **Colors by Station Type:**
- **Railway Stations** (`railway="station"`): Green (#22c55e)
- **Tram Stops** (`railway="tram_stop"`): Yellow (#eab308)
- **Unknown Types**: Gray (#6b7280)

#### **Styling Properties:**
- **Fill Transparency**: 10% opacity (0.1) for all isochrones
- **Border Width**: 
  - 30-minute duration: 2px border
  - All other durations: No border (0px)
- **Additional Properties**: Includes `railway-type` for identification

#### **Generated Durations:**
The application generates isochrones for multiple walking distances:
- 5 minutes
- 10 minutes  
- 15 minutes
- 20 minutes
- 30 minutes

### Isochrone Console Output

During area creation, you'll see progress for each station with styling applied:

```bash
🔍 Retrieving railway stations within 1000m of 41.9028, 12.4964...
✓ Area 'test-area' created successfully!
  📍 Generating isochrone data for station: Repubblica
    ✓ Saved 5min isochrone to: test-area/354964363/5min.json
    ✓ Saved 10min isochrone to: test-area/354964363/10min.json
    ✓ Saved 15min isochrone to: test-area/354964363/15min.json
    ✓ Saved 20min isochrone to: test-area/354964363/20min.json
    ✓ Saved 30min isochrone to: test-area/354964363/30min.json
  📍 Generating isochrone data for station: Termini (tram stop)
    ✓ Saved 5min isochrone to: test-area/610865305/5min.json
    ✓ Saved 10min isochrone to: test-area/610865305/10min.json
    ✓ Saved 15min isochrone to: test-area/610865305/15min.json
    ✓ Saved 20min isochrone to: test-area/610865305/20min.json
    ✓ Saved 30min isochrone to: test-area/610865305/30min.json
✓ Retrieved and stored 4 stations with isochrone data
```

With debug logging enabled, you'll also see styling information:
```bash
dbug: Added styling to isochrone: fill=#22c55e, stroke=#22c55e, opacity=0.1, width=0
dbug: Added styling to isochrone: fill=#eab308, stroke=#eab308, opacity=0.1, width=2
```

### Error Handling

The application gracefully handles isochrone generation errors:

- **Missing MapBox API Key**: Skips isochrone generation with warning
- **API Rate Limits**: Includes delays between API calls
- **Network Issues**: Logs errors but continues processing other stations
- **Invalid Responses**: Validates JSON structure before saving

## Area-Wide Isochrones

### Overview

Area-wide isochrones provide a unified view of transit accessibility across an entire area by combining all individual station isochrones into a single geometric representation.

### Generation Process

1. **Individual Station Isochrones**: First, isochrones are generated for each transit station (5, 10, 15, 20, 30 minutes)
2. **Collection**: All station isochrone files for a specific duration are collected from blob storage
3. **Geometric Union**: NetTopologySuite performs a geometric union operation to combine overlapping polygons
4. **Validation**: The resulting geometry is validated and fixed if necessary using buffer operations
5. **Storage**: The unified polygon is saved as GeoJSON at `/isochrone/{areaid}/{duration}min.json`

### File Structure

```
isochrone/
├── {areaid}/
│   ├── {stationid1}/
│   │   ├── 5min.json    # Individual station isochrone
│   │   ├── 10min.json
│   │   └── ...
│   ├── {stationid2}/
│   │   └── ...
│   ├── 5min.json        # Area-wide isochrone (union of all stations)
│   ├── 10min.json       # Area-wide isochrone
│   └── ...
```

### Technical Implementation

- **Library**: NetTopologySuite for geometric operations
- **Union Algorithm**: CascadedPolygonUnion for optimal performance with multiple polygons
- **Overlap Handling**: Automatic geometric union correctly handles overlapping areas
- **Styling**: Area-wide isochrones use blue color (#3b82f6) with 15% opacity
- **Properties**: GeoJSON includes metadata (type: "area-wide", duration, styling)

### Use Cases

- **Urban Planning**: Visualize combined transit accessibility across a city district
- **Real Estate**: Assess overall transit connectivity for property development
- **Transportation Analysis**: Identify gaps in transit coverage
- **Policy Making**: Evaluate the effectiveness of transit network coverage

## Configuration

### Setup Configuration

1. Copy `generator.config.json.template` to `generator.config.json`
2. Replace the placeholder values:
   - `<your-storage-account-name>`: Your Azure Storage account name
   - `<your-account-key>`: Your Azure Storage account key
   - `<azure-maps-subscription-key>`: Your Azure Maps API key (if using Azure Maps)
   - `<mapbox-subscription-key>`: Your MapBox API token (if using MapBox)
3. Set the `API` field to either `"Azure"` or `"MapBox"` depending on your preferred provider
4. Configure the `GeneratorSettings` section for your metro data:
   - `buildStations`: Enable/disable station generation
   - `distances`: Array of distances in meters for proximity calculations
   - `stations`: Output directory for generated station data
   - `metro`: Array of metro line definition files

### API Provider Configuration

The generator supports two mapping API providers:

**Azure Maps:**
- Set `"API": "Azure"` in AppSettings
- Provide your Azure Maps subscription key
- Best for enterprise scenarios with Azure integration

**MapBox:**
- Set `"API": "MapBox"` in AppSettings  
- Provide your MapBox access token
- Good for flexible mapping solutions

### Security Best Practices

**Development Environment:**
- Use connection strings with account keys for local development
- Never commit real connection strings to source control
- Use the template file for reference

**Production Environment:**
- Use Azure Managed Identity instead of connection strings
- Store secrets in Azure Key Vault
- Implement proper RBAC for storage access

## Usage

### Command Line Parameters

The generator supports the following command-line parameters:

```bash
generator [options]
```

**Options:**
- `-l, --logging <level>` - Set the minimum log level
  - Valid values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`
  - Default: `Information`
- `-h, --help` - Show detailed help information

**Examples:**
```bash
# Run with default Information logging
dotnet run

# Run with Debug logging level  
dotnet run -- --logging Debug

# Run with Warning logging level (short form)
dotnet run -- -l Warning

# Show help information
dotnet run -- --help
```

### Build the Application

```bash
dotnet build
```

### Run the Application

```bash
dotnet run
```

### Expected Output

```
Metro Proximity Generator Starting...
Log level set to: Information
Metro Proximity Generator
=========================
Configuration loaded successfully
Log Level: Information
Testing Azure Storage connection...
✓ Azure Storage connection test successful!
  Account Kind: StorageV2
  SKU Name: Standard_LRS

No valid Azure Maps API key configured. Skipping Azure Maps API test.
Testing MapBox API key...
✓ MapBox API key validation successful!
  Status: OK

Metro Proximity Generator Completed Successfully
```

The application will test:
- **Azure Storage connectivity** (if connection string is valid)
- **Azure Maps API key** (if configured and API is set to "Azure")
- **MapBox API key** (if configured and API is set to "MapBox")

### Error Handling

**Critical Service Failures:**
If any configured service fails validation, the application will exit with code 1:

```
❌ Azure Storage connection test failed (check configuration)
   Error: Settings must be of the form "name=value".
❌ Critical service validation failed. Please check your configuration.
```

**Exit Codes:**
- `0` - Success (all tests passed or were skipped)
- `1` - Failure (one or more critical service tests failed)

**Service Validation Logic:**
- **Not configured** = Skipped (not a failure)
- **Configured but invalid** = Critical failure (application exits)
- **Configured and valid** = Success (continues)

**With Debug Logging:**
```bash
dotnet run -- --logging Debug
```

**Help Output:**
```bash
dotnet run -- --help
```
Shows comprehensive usage information, examples, and configuration guidance.

## Project Structure

```
generator/
├── generator.csproj              # Project file with dependencies
├── Program.cs                    # Main application code
├── generator.config.json         # Configuration file (user-specific)
├── generator.config.json.template # Configuration template
└── README.md                     # This file
```

## Dependencies

- **Azure.Storage.Blobs** (12.21.2): Azure Blob Storage client library
- **Azure.Identity** (1.12.1): Azure authentication library
- **Microsoft.Extensions.Configuration** (8.0.0): Configuration framework
- **Microsoft.Extensions.Logging** (8.0.1): Logging framework
- **System.CommandLine** (2.0.0-beta4): Command-line parsing library

## Azure Storage Integration

The application includes optional Azure Storage connectivity testing:

- Loads connection string from configuration
- Tests connection using Azure Storage Blob client
- Displays account information if connection is successful
- Gracefully handles connection failures

## Error Handling

- Comprehensive exception handling
- Structured logging with different log levels
- Graceful degradation when Azure Storage is not configured
- Exit codes for automation scenarios

## Security Considerations

1. **Never hardcode credentials** in source code
2. **Use Azure Key Vault** for production secrets
3. **Prefer Managed Identity** over connection strings
4. **Implement least privilege** access policies
5. **Rotate keys regularly**
6. **Enable encryption** in transit and at rest

## Next Steps

To extend this application:

1. Add specific generator functionality for your use case
2. Implement Azure Storage operations (upload, download, list)
3. Add configuration for multiple environments
4. Integrate with Azure Key Vault for secret management
5. Add unit tests and integration tests


# AREAS

Naples, IT

```dotnet run -- area create naples --center 40.8585186,14.2543934 --diameter 20000 --displayname "Napoli" --logging debug```

Rome, IT
```dotnet run -- area create rome --center 41.8902142,12.489656 --diameter 45000 --displayname "Roma" --logging debug```

Milan, IT
```dotnet run -- area create milan --center 45.4627338,9.1777322 --diameter 15000 --displayname "Milano" --logging debug```

## Developer Mode Examples

For faster development and testing, you can use the `--developer` flag with any of the above commands:

```bash
# Quick test with Naples area (only 6 stations max)
dotnet run -- area create naples --center 40.8585186,14.2543934 --diameter 21000 --displayname "Napoli" --developer --noisochrone --logging debug

# Test Rome area with limited stations
dotnet run -- area create rome --center 41.8902142,12.489656 --diameter 45000 --displayname "Roma" --developer --noisochrone

# Test Milan area with limited stations and logging
dotnet run -- area create milan --center 45.4627338,9.1777322 --diameter 25000 --displayname "Milano" --developer --noisochrone --logging debug
```

