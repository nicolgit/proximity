namespace api.Models;

/// <summary>
/// Data transfer object for Area API response
/// </summary>
public class AreaDto
{
    public string Country { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Diameter { get; set; }
}
