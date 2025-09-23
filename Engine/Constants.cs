namespace Engine;

public static class App
{
    public const string Name = "webstir";
    public const string DevService = "Dev Service";
}

public static class Folders
{
    public const string Src = "src";
    public const string Build = "build";
    public const string Dist = "dist";
    public const string Tools = ".tools";
    public const string Tests = "tests";
    public const string Frontend = "frontend";
    public const string Backend = "backend";
    public const string Shared = "shared";
    public const string Types = "types";
    public const string App = "app";
    public const string Pages = "pages";
    public const string Styles = "styles";
    public const string Scripts = "scripts";
    public const string Images = "images";
    public const string Fonts = "fonts";
    public const string Media = "media";
    public const string Chunks = "chunks";
    public const string Home = "home";
    public const string NodeModules = "node_modules";
    public const string Seed = "seed";
    public const string Demo = "demo";
    public const string Temp = "temp";
}

public static class Files
{
    public const string PackageJson = "package.json";
    public const string PackageLockJson = "package-lock.json";
    public const string TsBuildInfo = ".tsbuildinfo";
    public const string BaseTsConfigJson = "base.tsconfig.json";
    public const string ManifestJson = "manifest.json";
    public const string FrontendManifestJson = "frontend-manifest.json";
    public const string Test = ".test";
    public const string Index = "index";
    public const string IndexHtml = "index.html";
    public const string RefreshJs = "refresh.js";
    public const string HmrJs = "hmr.js";
    public const string RobotsTxt = "robots.txt";
}

public static class FileExtensions
{
    public const string Html = ".html";
    public const string Css = ".css";
    public const string Br = ".br";
    public const string Gz = ".gz";
    public const string Dts = ".d.ts";
    public const string Ts = ".ts";
    public const string Js = ".js";
    public const string Map = ".map";
    public const string Png = ".png";
    public const string Jpg = ".jpg";
    public const string Jpeg = ".jpeg";
    public const string Gif = ".gif";
    public const string Svg = ".svg";
    public const string Webp = ".webp";
    public const string Ico = ".ico";
    public const string Woff = ".woff";
    public const string Woff2 = ".woff2";
    public const string Ttf = ".ttf";
    public const string Otf = ".otf";
    public const string Eot = ".eot";
    public const string Mp3 = ".mp3";
    public const string M4a = ".m4a";
    public const string Wav = ".wav";
    public const string Ogg = ".ogg";
    public const string Mp4 = ".mp4";
    public const string Webm = ".webm";
    public const string Mov = ".mov";
}

public static class Resources
{
    public const string Path = "Engine.Resources";
    public const string SrcPath = $"{Path}.{Folders.Src}";
    public const string FrontendPath = $"{SrcPath}.{Folders.Frontend}";
    public const string BackendPath = $"{SrcPath}.{Folders.Backend}";
    public const string SharedPath = $"{SrcPath}.{Folders.Shared}";
    public const string TypesPath = $"{Path}.types";
    public const string ToolsPath = $"{Path}.tools";
}
