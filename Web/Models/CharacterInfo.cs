namespace Narratum.Web.Models;

/// <summary>
/// Character information for Web UI components.
/// </summary>
public class CharacterInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> KnownFacts { get; set; } = new();
    public string CurrentLocation { get; set; } = "";
    public int EventCount { get; set; }
}
