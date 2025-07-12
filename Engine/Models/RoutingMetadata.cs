namespace Engine.Models;

public class RoutingMetadata
{
    public Dictionary<string, PageRouteInfo> Pages { get; set; } = [];
    public bool HasSpaPages => Pages.Any(p => p.Value.IsSpaEnabled);
}

public class PageRouteInfo
{
    public required string PageName { get; set; }
    public required string Route { get; set; }
    public bool IsSpaEnabled { get; set; }
    public required string TypeScriptPath { get; set; }
}