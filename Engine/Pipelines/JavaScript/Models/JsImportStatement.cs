using System.Collections.Generic;

namespace Engine.Pipelines.JavaScript.Models;

public class JsImportStatement
{
    public required string Source
    {
        get; set;
    }
    public string? ResolvedPath
    {
        get; set;
    }
    public JsImportType Type
    {
        get; set;
    }
    public List<string> Specifiers { get; set; } = [];
    public bool IsDynamic
    {
        get; set;
    }
    public int LineNumber
    {
        get; set;
    }
    public string? DefaultSpecifier
    {
        get; set;
    }
    public string? NamespaceSpecifier
    {
        get; set;
    }
}
