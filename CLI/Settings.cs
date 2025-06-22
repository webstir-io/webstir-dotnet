namespace CLI;

public static class Settings
{
    public static string SourceFolder { get; } = "src";
    public static string DistFolder { get; } = "dist";
    public static string BuildFolder { get; } = "build";
    public static string AppFolder { get; } = "app";
    public static string PagesFolder { get; } = "pages";
    public static string StylesFolder { get; } = "styles";
    public static string ScriptsFolder { get; } = "scripts";
    public static string ImagesFolder { get; } = "images";
    public static string ConfigFolder { get; } = "config";
    public static string IndexFolder {get; } = "index";
    public static string NodeModulesFolder { get; } = "node_modules";
}

public static class Directories
{
    public static DirectoryInfo SourceDirectory => Directory.CreateDirectory(Settings.SourceFolder);
    public static DirectoryInfo BuildDirectory => Directory.CreateDirectory(Settings.BuildFolder);
    public static DirectoryInfo DistDirectory => Directory.CreateDirectory(Settings.DistFolder);
    public static DirectoryInfo NodeModulesDirectory => new(Settings.NodeModulesFolder);
    public static DirectoryInfo AppDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.AppFolder));
    public static DirectoryInfo PagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo IndexDirectory => Directory.CreateDirectory(PagesDirectory.Join(Settings.IndexFolder));
    public static DirectoryInfo ImagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo ConfigDirectory => Directory.CreateDirectory(AppDirectory.Join(Settings.ConfigFolder));
    public static DirectoryInfo BuildPagesDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo BuildImagesDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo DistImagesDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo DistPagesDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.PagesFolder));

    public static DirectoryInfo SubDirectory(this DirectoryInfo directoryInfo, string subDirectory)
    {
        return directoryInfo.CreateSubdirectory(subDirectory);
    }

    public static string Join(this DirectoryInfo directoryInfo, string name)
    {
        return Path.Combine(directoryInfo.FullName, name);
    }

    public static string Join(this string path, string name)
    {
        return Path.Combine(path, name);
    }

    public static IEnumerable<FileInfo> GetFilesRecursively(this DirectoryInfo directoryInfo, string filter = "*")
    {
        return directoryInfo.EnumerateFiles(filter, SearchOption.AllDirectories);
    }

    public static void CopyTo(this DirectoryInfo sourceDirectory, string destPath, bool recursive = true)
    {
        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");

        var destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourceDirectory.FullName.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var file in sourceDirectory.GetFiles())
        {
            var targetFilePath = Path.Combine(destPath, file.Name);
            // Using CopyTo with overwrite flag to avoid FileSystemWatcher issues
            file.CopyTo(targetFilePath, overwrite: true);
        }

        if (recursive)
        {
            foreach (var subDirectory in sourceDirectory.GetDirectories())
            {
                var destDirectory = Path.Combine(destPath, subDirectory.Name);
                subDirectory.CopyTo(destDirectory, recursive);
            }
        }
    }
}