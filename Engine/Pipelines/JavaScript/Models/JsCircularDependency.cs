namespace Engine.Pipelines.JavaScript.Models;

public class JsCircularDependency
{
    public List<string> Modules { get; set; } = [];
    public string Path { get; set; } = string.Empty;
}