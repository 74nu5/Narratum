namespace Narratum.Web.Models;

/// <summary>
/// Location information for Web UI components.
/// </summary>
public class LocationInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public List<string> ConnectedLocations { get; set; } = new();
}
