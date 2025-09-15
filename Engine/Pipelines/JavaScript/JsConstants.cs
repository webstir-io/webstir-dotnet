namespace Engine.Pipelines.JavaScript;

public static class JsConstants
{
    // Process commands
    public const string TscCommand = "tsc";
    public const string NpmCommand = "npm";

    // Process arguments
    public const string TscBuildArg = "--build";
    public const string NpmCiArg = "ci";
    public const string NpmInstallArg = "install";

    // Esbuild configuration
    public const string EsbuildBinary = "esbuild";
    public const string EsbuildWindowsBinary = "esbuild.cmd";
    public const string EsbuildBinFolder = ".bin";

    // Esbuild arguments
    public const string EsbuildBundle = "--bundle";
    public const string EsbuildFormatEsm = "--format=esm";
    public const string EsbuildOutdir = "--outdir=";
    public const string EsbuildOutbase = "--outbase=";
    public const string EsbuildOutfile = "--outfile=";
    public const string EsbuildDefineNodeEnv = "--define:process.env.NODE_ENV=";
    public const string EsbuildSourcemap = "--sourcemap";
    public const string EsbuildAllowOverwrite = "--allow-overwrite";
    public const string EsbuildMinify = "--minify";
    public const string EsbuildDropConsole = "--drop:console";
    public const string EsbuildSplitting = "--splitting";
    public const string EsbuildChunkNames = "--chunk-names=";
    public const string EsbuildEntryNames = "--entry-names=";
    public const string EsbuildMetafile = "--metafile=";

    // Environment values
    public const string EnvDevelopment = "development";
    public const string EnvProduction = "production";

    // Error messages
    public const string TypeScriptCompilationFailed = "TypeScript compilation failed";
    public const string EsbuildFailed = "esbuild failed";
    public const string EsbuildNotFoundPrefix = "esbuild not found at ";
    public const string EsbuildNotFoundSuffix = ". Please run 'npm install' to install dependencies.";
    public const string FailedToStartEsbuild = "Failed to start esbuild process";
    public const string FailedToStartFormat = "Failed to start {0} process.";

    // Process descriptions
    public const string TypeScriptCompilationDesc = "TypeScript compilation";
    public const string NpmInstallDesc = "npm install";

    // Log messages
    public const string RefreshJsNotFoundLog = "{RefreshJsFile} not found in {SourcePath}";

    // Error message formatting
    public const string ProcessFailedFormat = "{0} failed (Exit Code: {1})";
    public const string EsbuildFailedFormat = "esbuild failed (exit {0}):\n{1}\n{2}";
    public const string ErrorsHeader = "\nErrors:\n";
    public const string OutputHeader = "\nOutput:\n";

    // File names
    public const string ErrorJs = "error.js";
    public const string TempFolder = "temp";
    public const string SingleFolder = "single";
    public const string MetaJson = "meta.json";

    // Esbuild output patterns
    public const string ChunkNamePattern = "[name].[hash]";
    public const string EntryNamePattern = "[name].[hash]";
    public const string EntryDirPattern = "[dir]";
}
