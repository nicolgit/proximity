# Generator - Metro Proximity Generator

A .NET 8 console application for managing areas and generating station proximity data using Azure Maps or MapBox APIs.

## Prerequisites

- .NET 8.0 SDK
- Azure Storage Account (for area storage, station data, and isochrone polygons)
- MapBox API Key (for geographical services and isochrone generation)

## Quick Reference

```bash
# Area Management
dotnet run -- area create italy/rome --center "41.9028,12.4964" --diameter 1000 --displayname "Rome City Center"
dotnet run -- area create italy/rome --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug
dotnet run -- area isochrone italy/romerome
dotnet run -- area isochrone italy/romerome --delete
dotnet run -- area delete "italy/rome"
dotnet run -- area list

# Station Management  
dotnet run -- station list italy/romerome
dotnet run -- station list rome --filter Roma
dotnet run -- station isochrone italy/rome
dotnet run -- station isochrone italy/milan --delete
dotnet run -- station isochrone italy/milan 21226369
dotnet run -- station isochrone italy/milan 21226369 --delete
dotnet run -- station isochrone italy/milan 21226369 --delete 10

# Global Options
dotnet run -- --logging Debug
dotnet run -- --help
```
 