namespace Engine.Extensions;

public static class DirectoryExtensions
{
    public static string CombinePath(this DirectoryInfo directoryInfo, params string[] paths)
    {
        var allPaths = new string[paths.Length + 1];
        allPaths[0] = directoryInfo.FullName;
        paths.CopyTo(allPaths, 1);
        return Path.Combine(allPaths);
    }

    public static DirectoryInfo CreateSubDirectory(this DirectoryInfo directoryInfo, string subDirectory)
    {
        return Directory.CreateDirectory(directoryInfo.CombinePath(subDirectory));
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