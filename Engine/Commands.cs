namespace Engine;

public static class Commands
{
    public const string Init = "init";
    public const string AddPage = "add-page";
    public const string AddTest = "add-test";
    public const string Build = "build";
    public const string Test = "test";
    public const string Watch = "watch";
    public const string Publish = "publish";
    public const string Install = "install";
    public const string Toolchain = "toolchain";
    public const string Help = "help";
}

public static class HelpOptions
{
    public const string Help = "--help";
    public const string HelpShort = "-h";
}

public static class ProjectOptions
{
    public const string ProjectName = "--project-name";
    public const string ProjectNameShort = "-p";
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

public static class InstallOptions
{
    public const string DryRun = "--dry-run";
}
