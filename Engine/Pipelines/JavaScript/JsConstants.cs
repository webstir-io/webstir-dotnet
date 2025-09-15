namespace Engine.Pipelines.JavaScript;

public static class JsConstants
{
    // Process commands
    public const string TscCommand = "tsc";

    // Process arguments
    public const string TscBuildArg = "--build";

    // Error messages
    public const string TypeScriptCompilationFailed = "TypeScript compilation failed";
    public const string FailedToStartFormat = "Failed to start {0} process.";

    // Process descriptions
    public const string TypeScriptCompilationDesc = "TypeScript compilation";

    // Log messages
    public const string RefreshJsNotFoundLog = "{RefreshJsFile} not found in {SourcePath}";

    // Error message formatting
    public const string ProcessFailedFormat = "{0} failed (Exit Code: {1})";
    public const string ErrorsHeader = "\nErrors:\n";
    public const string OutputHeader = "\nOutput:\n";

    // File names
    public const string ErrorJs = "error.js";
    public const string SingleFolder = "single";
}
