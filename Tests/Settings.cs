namespace Tests;

public static class Settings
{
    public static string TempFolder { get; } = "temp";
    public static string DemosFolder { get; } = "demos";
}

public static class Directories
{
    public static DirectoryInfo TempDirectory => Directory.CreateDirectory(Settings.TempFolder);
    public static string GetTestDirectory(string testName) => Path.Combine(TempDirectory.FullName, testName);
}
