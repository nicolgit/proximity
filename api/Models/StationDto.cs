namespace api.Models;

/// <summary>
/// Data transfer object for Station API response
/// </summary>
public class StationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? WikipediaLink { get; set; }
}
