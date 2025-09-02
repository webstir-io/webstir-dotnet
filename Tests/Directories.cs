namespace Tests;

public static class Directories
{
    public static DirectoryInfo OutDirectory => Directory.CreateDirectory(Settings.OutFolder);
}

