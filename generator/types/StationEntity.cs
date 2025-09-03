using Azure;
using Azure.Data.Tables;

namespace Generator.Types;

public class StationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Will be the area name
    public string RowKey { get; set; } = string.Empty; // Will be the station ID
    public string Name { get; set; } = string.Empty; // Station name
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? WikipediaLink { get; set; }
    public string Railway { get; set; } = string.Empty; // Type of railway (station, tram_stop, etc.)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
