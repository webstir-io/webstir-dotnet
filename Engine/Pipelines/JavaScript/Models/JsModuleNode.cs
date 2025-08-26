namespace Engine.Pipelines.JavaScript.Models;

public class JsModuleNode
{
    public required string FilePath { get; set; }
    public HashSet<string> Dependencies { get; set; } = [];
    public HashSet<string> Dependents { get; set; } = [];
    public JsModuleType Type { get; set; }
    public bool IsEntryPoint { get; set; }
    public JsModuleInfo? Info { get; set; }
}