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
    public const string Client = "client";
    public const string Server = "server";
    public const string Shared = "shared";
    public const string App = "app";
    public const string Pages = "pages";
    public const string Styles = "styles";
    public const string Scripts = "scripts";
    public const string Images = "images";
    public const string Home = "home";
    public const string NodeModules = "node_modules";
    public const string Seed = "seed";
    public const string Demo = "demo";
}

public static class Files
{
    public const string PackageJson = "package.json";
    public const string PackageLockJson = "package-lock.json";
    public const string TsBuildInfo = ".tsbuildinfo";
    public const string Index = "index";
    public const string IndexHtml = "index.html";
    public const string RefreshJs = "refresh.js";
}

public static class Routes
{
    public const string Sse = "/sse";
    public const string Api = "/api";
    public const string Home = "/home";
}

public static class FileExtensions
{
    public const string Html = ".html";
    public const string Css = ".css";
    public const string Js = ".js";
    public const string Png = ".png";
    public const string Jpg = ".jpg";
    public const string Jpeg = ".jpeg";
    public const string Gif = ".gif";
    public const string Svg = ".svg";
    public const string Webp = ".webp";
    public const string Ico = ".ico";
}

public static class CacheHeaders
{
    public const string NoCache = "no-cache, no-store, must-revalidate";
    public const string NoCacheMustRevalidate = "no-cache, must-revalidate";
    public const string LongCache = "public, max-age=31536000, immutable";
    public const string PragmaNoCache = "no-cache";
    public const string ExpiresZero = "0";
}

public static class Templates
{
    public const string Path = "Engine.Templates";
    public const string SrcPath = $"{Path}.{Folders.Src}";
    public const string ClientPath = $"{SrcPath}.{Folders.Client}";
    public const string ServerPath = $"{SrcPath}.{Folders.Server}";
    public const string SharedPath = $"{SrcPath}.{Folders.Shared}";
}