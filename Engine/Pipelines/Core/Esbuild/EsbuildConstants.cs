namespace Engine.Pipelines.Core.Esbuild;

public static class EsbuildConstants
{
    // Esbuild binary configuration
    public const string Binary = "esbuild";
    public const string WindowsBinary = "esbuild.cmd";
    public const string BinFolder = ".bin";

    // Format types
    public const string FormatEsm = "esm";

    // Loader types
    public const string LoaderCss = "css";
    public const string LoaderLocalCss = "local-css";

    // File extensions
    public const string ExtCss = ".css";
    public const string ExtModuleCss = ".module.css";

    // Folders
    public const string ChunksFolder = "chunks";

    // Esbuild command arguments
    public const string Bundle = "--bundle";
    public const string Outdir = "--outdir=";
    public const string Outbase = "--outbase=";
    public const string Outfile = "--outfile=";
    public const string DefineNodeEnv = "--define:process.env.NODE_ENV=";
    public const string Sourcemap = "--sourcemap";
    public const string AllowOverwrite = "--allow-overwrite";
    public const string Minify = "--minify";
    public const string DropConsole = "--drop:console";
    public const string Splitting = "--splitting";
    public const string ChunkNames = "--chunk-names=";
    public const string EntryNames = "--entry-names=";
    public const string Metafile = "--metafile=";

    // Environment values
    public const string EnvDevelopment = "development";
    public const string EnvProduction = "production";

    // Error messages
    public const string Failed = "esbuild failed";
    public const string NotFoundPrefix = "esbuild not found at ";
    public const string NotFoundSuffix = ". Please run 'npm install' to install dependencies.";
    public const string FailedToStart = "Failed to start esbuild process";
    public const string FailedFormat = "esbuild failed (exit {0}):\n{1}\n{2}";

    // File names
    public const string MetaJson = "meta.json";

    // Output patterns
    public const string ChunkNamePattern = "[name].[hash]";
    public const string EntryNamePattern = "[name].[hash]";
    public const string EntryDirPattern = "[dir]";

    // File patterns
    public const string CssFilePattern = "*.css";
}
