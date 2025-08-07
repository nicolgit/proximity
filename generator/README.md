# Generator - Metro Proximity Generator

A .NET 8 console application for managing areas and generating station proximity data using Azure Maps or MapBox APIs.

## Prerequisites

- .NET 8.0 SDK
- Azure Storage Account (for area storage, station data, and isochrone polygons)
- MapBox API Key (for geographical services and isochrone generation)

## Quick Reference

```bash
# Area Management
dotnet run -- area create rome --center "41.9028,12.4964" --diameter 1000 --displayname "Rome City Center"
dotnet run -- area create rome --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug
dotnet run -- area create "stations-only" --center "41.9028,12.4964" --diameter 1000 --displayname "Stations Only" --noisochrone
dotnet run -- area isochrone rome
dotnet run -- area isochrone rome --delete
dotnet run -- area delete "rome-center"
dotnet run -- area list

# Station Management  
dotnet run -- station list rome
dotnet run -- station list rome --filter Roma
dotnet run -- station isochrone rome
dotnet run -- station isochrone milan 21226369
dotnet run -- station isochrone milan 21226369 --delete
dotnet run -- station isochrone milan 21226369 --delete 10

# Global Options
dotnet run -- --logging Debug
dotnet run -- --help
```



