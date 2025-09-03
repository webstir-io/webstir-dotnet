namespace Engine.Pipelines.JavaScript.Models;

public class JsTransformContext
{
    public required int ModuleId
    {
        get; init;
    }
    public required string FilePath
    {
        get; init;
    }
    public required bool EnableScopeHoisting
    {
        get; init;
    }
    public required bool EnableTreeShaking
    {
        get; init;
    }
}
