namespace Engine.Pipelines.JavaScript.Models;

public class JsExportStatement
{
    public JsExportType Type { get; set; }
    public List<string> Specifiers { get; set; } = [];
    public string? Source { get; set; }
    public int LineNumber { get; set; }
    public bool IsDefault { get; set; }
}