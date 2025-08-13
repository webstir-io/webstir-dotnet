namespace Engine;

public static class App
{
    public const string Name = "webstir";
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
    public const string Index = "index";
    public const string Home = "home";
    public const string NodeModules = "node_modules";
    public const string Seed = "seed";
    public const string Demo = "demo";
}

public static class Files
{
    public const string PackageJson = "package.json";
    public const string tsBuildInfo = ".tsbuildinfo";

}

public static class Resources
{
    public const string SrcResourcesPath = $"{Folders.Src}";
    public const string ClientResourcesPath = $"{SrcResourcesPath}.{Folders.Client}";
    public const string ServerResourcesPath = $"{SrcResourcesPath}.{Folders.Server}";
    public const string SharedResourcesPath = $"{SrcResourcesPath}.{Folders.Shared}";
}