# Area API Documentation

## Overview

The Area API provides endpoints to retrieve area information from Azure Table Storage. Each area contains location and geographic information including coordinates and diameter.

## Endpoints

### GET /api/area
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

### GET /api/area/{id}
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

### GET /api/area/{id}/station
Returns all stations for a specific area.

**Parameters:**
- `id` (string): The unique identifier for the area

**Response Format:**
```json
[
  {
    "id": "station1",
    "name": "Central Station",
    "latitude": 40.7580,
    "longitude": -73.9855
  },
  {
    "id": "station2",
    "name": "North Station", 
    "latitude": 40.7614,
    "longitude": -73.9776
  }
]
```

### GET /api/area/{id}/station/{stationid}/isochrone/{time}
Returns isochrone data for a specific station within an area for a given time duration.

**Parameters:**
- `id` (string): The unique identifier for the area
- `stationid` (string): The unique identifier for the station
- `time` (string): Time duration in minutes. Must be one of: `10`, `15`, `20`, `30`

**Response Format:**
Returns the JSON content from the blob `isochrone/{id}/{stationid}/{time}min.json`. The exact format depends on the stored isochrone data structure.

**Example Request:**
```
GET /api/area/milan/station/central/15
```

**Example Response:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[...coordinates...]]]
      },
      "properties": {
        "time": 15,
        "station": "central"
      }
    }
  ]
}
```
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

### Error Responses for All Endpoints

**Common Error Responses:**
- `400 Bad Request`: Invalid or missing required parameters
- `404 Not Found`: Requested resource not found
- `503 Service Unavailable`: Storage service unavailable
- `500 Internal Server Error`: Unexpected server error

**Specific to Isochrone Endpoint (/api/area/{id}/station/{stationid}/{time}):**
- `400 Bad Request`: Time parameter must be one of: 10, 15, 20, 30
- `404 Not Found`: Isochrone data not found for the specified parameters

## Storage Schema

### Azure Table Storage

The API reads from an Azure Table Storage table named "areas" with the following entity structure:

- **PartitionKey**: "area" (constant for all area entities)
- **RowKey**: Unique identifier for the area (maps to `id` in response)
- **Name**: Internal name for the area
- **DisplayName**: User-friendly display name (preferred over Name in response)
- **Latitude**: Decimal latitude coordinate
- **Longitude**: Decimal longitude coordinate  
- **DiameterMeters**: Diameter of the area in meters (maps to `diameter` in response)

### Azure Blob Storage

The isochrone endpoint reads from Azure Blob Storage:

- **Container**: `isochrone`
- **Blob Path Pattern**: `{areaId}/{stationId}/{time}min.json`
- **Content Type**: JSON files containing isochrone data
- **Example Path**: `isochrone/milan/central/15min.json`

The isochrone JSON files contain geographic data representing areas reachable within the specified time duration from a station.

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
   - Get all areas: `GET http://localhost:7071/api/area`
   - Get specific area: `GET http://localhost:7071/api/area/{id}`
   - Get stations by area: `GET http://localhost:7071/api/area/{id}/station`
   - Get isochrone data: `GET http://localhost:7071/api/area/{id}/station/{stationid}/{time}`

3. Example isochrone requests:
   ```bash
   # Get 15-minute isochrone for central station in milan area
   curl http://localhost:7071/api/area/milan/station/central/15
   
   # Get 30-minute isochrone
   curl http://localhost:7071/api/area/milan/station/central/30
   
   # Invalid time parameter (will return 400)
   curl http://localhost:7071/api/area/milan/station/central/25
   ```

## Sample Data Structure

### Table Storage Data

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

### Blob Storage Data

For isochrone data, create JSON files in the `isochrone` container following the path pattern `{areaId}/{stationId}/{time}min.json`. Example structure:

**File Path**: `isochrone/milan/central/15min.json`
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Polygon",
        "coordinates": [
          [
            [9.1900, 45.4642],
            [9.1950, 45.4680],
            [9.2000, 45.4642],
            [9.1900, 45.4642]
          ]
        ]
      },
      "properties": {
        "time": 15,
        "station": "central",
        "area": "milan"
      }
    }
  ]
}
```

Required blob files for each station:
- `{areaId}/{stationId}/10min.json`
- `{areaId}/{stationId}/15min.json`
- `{areaId}/{stationId}/20min.json`
- `{areaId}/{stationId}/30min.json`
