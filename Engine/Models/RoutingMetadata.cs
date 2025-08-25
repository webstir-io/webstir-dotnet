namespace Engine.Models;

public class RoutingMetadata
{
    public Dictionary<string, PageRouteInfo> Pages { get; set; } = [];
    public bool HasGlobalRouter { get; set; }
    public bool HasSpaPages => HasGlobalRouter || Pages.Any(p => p.Value.IsSpaEnabled);
}

public class PageRouteInfo
{
    public required string PageName { get; set; }
    public required string Route { get; set; }
    public bool IsSpaEnabled { get; set; }
    public required string TypeScriptPath { get; set; }
}