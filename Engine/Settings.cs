namespace Engine;

public static class Settings
{
    public static string SourceFolder { get; } = "src";
    public static string ClientFolder { get; } = "client";
    public static string ServerFolder { get; } = "server";
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
    public static string PackageJsonFile { get; } = "package.json";
    public static string SharedFolder { get; } = "shared";
    public static string DemoFolder { get; } = "demo";
    
    // Current working directory for webstir operations
    private static string _workingDirectory = Directory.GetCurrentDirectory();
    public static string WorkingDirectory 
    { 
        get => _workingDirectory;
        set => _workingDirectory = Path.GetFullPath(value);
    }
}

public static class Directories
{
    // Core directories that are always created
    public static DirectoryInfo SourceDirectory => Directory.CreateDirectory(Path.Combine(Settings.WorkingDirectory, Settings.SourceFolder));
    public static DirectoryInfo BuildDirectory => Directory.CreateDirectory(Path.Combine(Settings.WorkingDirectory, Settings.BuildFolder));
    public static DirectoryInfo DistDirectory => Directory.CreateDirectory(Path.Combine(Settings.WorkingDirectory, Settings.DistFolder));
    public static DirectoryInfo NodeModulesDirectory => new(Path.Combine(Settings.WorkingDirectory, Settings.NodeModulesFolder));
    
    // Helper methods to get directory info without creating
    public static DirectoryInfo GetClientDirectory() => new(SourceDirectory.Join(Settings.ClientFolder));
    public static DirectoryInfo GetServerDirectory() => new(SourceDirectory.Join(Settings.ServerFolder));
    public static DirectoryInfo GetSharedDirectory() => new(SourceDirectory.Join(Settings.SharedFolder));
    
    // Legacy directories (src/app, src/pages, src/images)
    public static DirectoryInfo AppDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.AppFolder));
    public static DirectoryInfo PagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo IndexDirectory => Directory.CreateDirectory(PagesDirectory.Join(Settings.IndexFolder));
    public static DirectoryInfo ImagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo ConfigDirectory => Directory.CreateDirectory(AppDirectory.Join(Settings.ConfigFolder));
    public static DirectoryInfo BuildPagesDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo BuildImagesDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo DistImagesDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo DistPagesDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.PagesFolder));

    // Client-specific directories (src/client/app, src/client/pages, src/client/images)
    public static DirectoryInfo ClientDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.ClientFolder));
    public static DirectoryInfo ClientAppDirectory => Directory.CreateDirectory(ClientDirectory.Join(Settings.AppFolder));
    public static DirectoryInfo ClientPagesDirectory => Directory.CreateDirectory(ClientDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo ClientIndexDirectory => Directory.CreateDirectory(ClientPagesDirectory.Join(Settings.IndexFolder));
    public static DirectoryInfo ClientImagesDirectory => Directory.CreateDirectory(ClientDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo ClientConfigDirectory => Directory.CreateDirectory(ClientAppDirectory.Join(Settings.ConfigFolder));
    public static DirectoryInfo ClientBuildDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.ClientFolder));    
    public static DirectoryInfo ClientBuildPagesDirectory => Directory.CreateDirectory(ClientBuildDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo ClientBuildImagesDirectory => Directory.CreateDirectory(ClientBuildDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo ClientDistDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.ClientFolder));    
    public static DirectoryInfo ClientDistImagesDirectory => Directory.CreateDirectory(ClientDistDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo ClientDistPagesDirectory => Directory.CreateDirectory(ClientDistDirectory.Join(Settings.PagesFolder));

    //Server-specific directories (src/server/app, src/server/pages, src/server/images)
    public static DirectoryInfo ServerDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.ServerFolder));
    public static DirectoryInfo ServerBuildDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.ServerFolder));
    public static DirectoryInfo ServerDistDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.ServerFolder));    

    // Shared directory (src/shared)
    public static DirectoryInfo SharedDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.SharedFolder));

    public static DirectoryInfo SubDirectory(this DirectoryInfo directoryInfo, string subDirectory)
    {
        // Use Directory.CreateDirectory to preserve hyphens in directory names
        // CreateSubdirectory can sometimes sanitize names, replacing hyphens with underscores
        var fullPath = Path.Combine(directoryInfo.FullName, subDirectory);
        return Directory.CreateDirectory(fullPath);
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