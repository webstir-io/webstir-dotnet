namespace CLI;

public static class Settings
{
    public static string SourceFolder { get; } = "src";
    public static string DistFolder { get; } = "dist";
    public static string BuildFolder { get; } = "build";
    public static string BinFolder { get; } = "bin";
    public static string AppFolder { get; } = "app";
    public static string PagesFolder { get; } = "pages";
    public static string StylesFolder { get; } = "styles";
    public static string ScriptsFolder { get; } = "scripts";
    public static string ImagesFolder { get; } = "images";
    public static string IndexFolder {get; } = "index";
}

public static class Directories
{
    public static DirectoryInfo SourceDirectory => Directory.CreateDirectory(Settings.SourceFolder);
    public static DirectoryInfo BuildDirectory => Directory.CreateDirectory(Settings.BuildFolder);
    public static DirectoryInfo DistDirectory => Directory.CreateDirectory(Settings.DistFolder);
    public static DirectoryInfo AppDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.AppFolder));
    public static DirectoryInfo PagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo IndexDirectory => Directory.CreateDirectory(PagesDirectory.Join(Settings.IndexFolder));
    public static DirectoryInfo ImagesDirectory => Directory.CreateDirectory(SourceDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo BuildPagesDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.PagesFolder));
    public static DirectoryInfo BinDirectory => Directory.CreateDirectory(BuildDirectory.Join(Settings.BinFolder));
    public static DirectoryInfo BinImagesDirectory => Directory.CreateDirectory(BinDirectory.Join(Settings.ImagesFolder));
    public static DirectoryInfo DistImagesDirectory => Directory.CreateDirectory(DistDirectory.Join(Settings.ImagesFolder));

    public static DirectoryInfo SubDirectory(this DirectoryInfo directoryInfo, string subDirectory)
    {
        return directoryInfo.CreateSubdirectory(subDirectory);
    }

    public static string Join(this DirectoryInfo directoryInfo, string name)
    {
        return $"{directoryInfo.FullName}/{name}";
    }

    public static string Join(this string path, string name)
    {
        return $"{path}/{name}";
    }

    public static IEnumerable<FileInfo> GetFilesRecursively(this DirectoryInfo directoryInfo, string filter = "*")
    {
        var foundFiles = new List<FileInfo>();
        foreach (var file in directoryInfo.GetFiles(filter))
        {
            foundFiles.Add(file);
        }

        foreach (var subDirectory in directoryInfo.GetDirectories())
            foundFiles.AddRange(subDirectory.GetFilesRecursively(filter));

        return foundFiles;
    }

    public static void CopyTo(this DirectoryInfo sourceDirectory, string destPath, bool recursive = true)
    {
        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");

        Directory.CreateDirectory(destPath);

        foreach (var file in sourceDirectory.GetFiles())
        {
            var targetFilePath = $"{destPath}/{file.Name}";
            //Wow, this triggers an infinite loop in FileSystemEvents.
            //file.CopyTo(targetFilePath);
            var fileLines = File.ReadAllBytes(file.FullName);
            File.WriteAllBytes(targetFilePath, fileLines);
        }

        var subDirectories = sourceDirectory.GetDirectories();
        if (recursive)
        {
            foreach (var subDirectory in subDirectories)
            {
                var destDirectory = $"{destPath}/{subDirectory.Name}";
                subDirectory.CopyTo(destDirectory);
            }
        }
    }
}