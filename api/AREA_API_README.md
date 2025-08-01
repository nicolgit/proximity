# Area API Documentation

## Overview

The Area API provides endpoints to retrieve area information from Azure Table Storage. Each area contains location and geographic information including coordinates and diameter.

## Endpoints

### GET /api/areas
Returns all areas from the storage table.

**Response Format:**
```json
[
  {
    "id": "area1",
    "name": "Downtown",
    "latitude": 40.7128,
    "longitude": -74.0060,
    "diameter": 1000
  },
  {
    "id": "area2", 
    "name": "Central Park",
    "latitude": 40.7829,
    "longitude": -73.9654,
    "diameter": 2500
  }
]
```

### GET /api/areas/{id}
Returns a specific area by ID.

**Parameters:**
- `id` (string): The unique identifier for the area

**Response Format:**
```json
{
  "id": "area1",
  "name": "Downtown",
  "latitude": 40.7128,
  "longitude": -74.0060,
  "diameter": 1000
}
```

**Error Responses:**
- `400 Bad Request`: Invalid or missing area ID
- `404 Not Found`: Area with specified ID not found
- `503 Service Unavailable`: Storage service unavailable
- `500 Internal Server Error`: Unexpected server error

## Storage Schema

The API reads from an Azure Table Storage table named "areas" with the following entity structure:

- **PartitionKey**: "area" (constant for all area entities)
- **RowKey**: Unique identifier for the area (maps to `id` in response)
- **Name**: Internal name for the area
- **DisplayName**: User-friendly display name (preferred over Name in response)
- **Latitude**: Decimal latitude coordinate
- **Longitude**: Decimal longitude coordinate  
- **DiameterMeters**: Diameter of the area in meters (maps to `diameter` in response)

## Configuration

The API uses the `CustomStorageConnectionString` configuration value from `local.settings.json` or application settings to connect to Azure Storage.

For local development, ensure your `local.settings.json` contains:
```json
{
  "Values": {
    "CustomStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=your-account;AccountKey=your-key;EndpointSuffix=core.windows.net"
  }
}
```

## Testing

To test the endpoints locally:

1. Start the Azure Functions runtime:
   ```bash
   dotnet run --project api/api.csproj
   ```

2. Access the endpoints:
   - Get all areas: `GET http://localhost:7071/api/areas`
   - Get specific area: `GET http://localhost:7071/api/areas/{id}`

## Sample Data Structure

To populate test data, you can use Azure Storage Explorer or Azure CLI to create entities in the "areas" table with this structure:

```json
{
  "PartitionKey": "area",
  "RowKey": "downtown-nyc",
  "Name": "downtown",
  "DisplayName": "Downtown Manhattan", 
  "Latitude": 40.7128,
  "Longitude": -74.0060,
  "DiameterMeters": 1000
}
```
