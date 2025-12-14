namespace Engine;

public static class Commands
{
    public const string Init = "init";
    public const string Repair = "repair";
    public const string AddPage = "add-page";
    public const string AddTest = "add-test";
    public const string AddRoute = "add-route";
    public const string AddJob = "add-job";
    public const string Enable = "enable";
    public const string Build = "build";
    public const string Test = "test";
    public const string Watch = "watch";
    public const string Publish = "publish";
    public const string BackendInspect = "backend-inspect";
    public const string Install = "install";
    public const string Smoke = "smoke";
    public const string Help = "help";
}

public static class HelpOptions
{
    public const string Help = "--help";
    public const string HelpShort = "-h";
}

public static class ProjectOptions
{
    public const string ProjectName = "--project";
    public const string ProjectNameShort = "-p";
}

public static class InitOptions
{
    public const string ClientOnly = "--client-only";
    public const string ServerOnly = "--server-only";
}

public static class InitModes
{
    public const string Full = "full";
    public const string Ssg = "ssg";
    public const string Spa = "spa";
    public const string Api = "api";
}

public static class BuildOptions
{
    public const string Clean = "--clean";
}

public static class InstallOptions
{
    public const string DryRun = "--dry-run";
    public const string Clean = "--clean";
    public const string PackageManager = "--package-manager";
    public const string PackageManagerShort = "-m";
}

public static class TestOptions
{
    public const string Runtime = "--runtime";
    public const string RuntimeShort = "-r";
}

public static class RepairOptions
{
    public const string DryRun = "--dry-run";
}
