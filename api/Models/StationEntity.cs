using Azure;
using Azure.Data.Tables;

namespace api.Models;

/// <summary>
/// Entity representing a Station in Azure Table Storage
/// </summary>
public class StationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Area ID
    public string RowKey { get; set; } = string.Empty; // Station ID
    public string Name { get; set; } = string.Empty; // Station name
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? WikipediaLink { get; set; }
    public string Railway { get; set; } = string.Empty; // Type of railway
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
