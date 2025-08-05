# Generator - Metro Proximity Generator

A .NET 8 console application for managing areas and generating station proximity data using Azure Maps or MapBox APIs.

## Features

- .NET 8 console application
- Area management with Azure Table Storage
- Configuration management using JSON files
- Azure Storage Blob and Table client integration
- MapBox API integration for geographical data
- Comprehensive logging with configurable levels
- Error handling with retry logic
- Security best practices
- Command-line interface with System.CommandLine

## Prerequisites

- .NET 8.0 SDK
- Azure Storage Account (for area storage and output data)
- MapBox API Key (for geographical services)

## Commands

### Area Management

#### Create Area
Create a new area with center coordinates and diameter:

```bash
dotnet run -- area create <name> --center <latitude,longitude> --diameter <meters> --displayname <display_name> [--developer]
```

**Parameters:**
- `<name>`: Name of the area (required)
- `--center`: Center coordinates as 'latitude,longitude' (required)
- `--diameter`: Diameter in meters (required)
- `--displayname`: Display name for the area (required)
- `--developer`: Developer mode - limit to first 3 railway stations and 3 tram stops (optional)

**Examples:**
```bash
# Create an area centered in Rome with 1km diameter
dotnet run -- area create "rome-center" --center "41.9028,12.4964" --diameter 1000 --displayname "Rome City Center"

# Create an area at equator with 500m diameter in developer mode
dotnet run -- area create "equator-point" --center "0,0" --diameter 500 --displayname "Equator Reference Point" --developer

# Create an area with debug logging and developer mode
dotnet run -- area create "test" --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug
```

#### Delete Area
Delete an existing area:

```bash
dotnet run -- area delete <name>
```

**Parameters:**
- `<name>`: Name of the area to delete (required)

**Examples:**
```bash
# Delete the Rome area
dotnet run -- area delete "rome-center"
```



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
✓ Retrieved and stored 45 stations

# Developer mode output  
🔧 Developer mode: limiting to first 3 railway stations and 3 tram stops
✓ Retrieved and stored 6 stations
  🔧 Developer mode: limited to 3 railway stations and 3 tram stops
```

### When to Use Developer Mode

- **Initial Development**: Testing area creation functionality
- **Configuration Testing**: Verifying Azure Storage and API connections
- **Debugging**: Investigating issues without processing large datasets
- **Rapid Iteration**: Quickly testing different coordinates and parameters

**Note**: Developer mode only affects the `area create` command. Other commands work normally.

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
dotnet run -- area create naples-dev --center 40.8585186,14.2543934 --diameter 20000 --displayname "Napoli Dev" --developer --logging debug

# Test Rome area with limited stations
dotnet run -- area create rome-dev --center 41.8902142,12.489656 --diameter 45000 --displayname "Roma Dev" --developer

# Test Milan area with limited stations and logging
dotnet run -- area create milan-dev --center 45.4627338,9.1777322 --diameter 20000 --displayname "Milano Dev" --developer --logging debug
```

