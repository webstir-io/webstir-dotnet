namespace Engine;

public static class Commands
{
    public const string Init = "init";
    public const string AddPage = "add-page";
    public const string Build = "build";
    public const string Watch = "watch";
    public const string Publish = "publish";
    public const string Help = "help";
    public const string Demo = "demo";
}

public static class HelpOptions
{
    public const string Help = "--help";
    public const string HelpShort = "-h";
}

public static class InitOptions
{
    public const string ClientOnly = "--client-only";
    public const string ServerOnly = "--server-only";
}

public static class BuildOptions
{
    public const string Clean = "--clean";
}