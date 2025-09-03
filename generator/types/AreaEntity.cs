using Azure;
using Azure.Data.Tables;

namespace Generator.Types;

public class AreaEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "area";
    public string RowKey { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int DiameterMeters { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
}