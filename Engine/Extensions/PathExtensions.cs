namespace Engine.Extensions;

public static class PathExtensions
{
    public static string Combine(this string path, params string[] segments)
    {
        foreach (var segment in segments)
            path = Path.Combine(path, segment);

        return path;
    }

    public static string Create(this string path)
    {
        return Directory.CreateDirectory(path).FullName;
    }

    public static string[] Folders(this string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        return Directory.GetDirectories(path);
    }

    public static string[] Files(this string path, string searchPattern = "*.*")
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        return Directory.GetFiles(path, searchPattern);
    }

    public static string Name(this string path)
    {
        return Path.GetFileName(path);
    }

    public static bool Exists(this string path)
    {
        return Path.Exists(path);
    }

    public static void CopyTo(this string sourcePath, string destPath, bool recursive = true)
    {
        if (!sourcePath.Exists())
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

        var destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourcePath.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var file in sourcePath.Files())
        {
            var targetFilePath = destPath.Combine(file);
            // Using CopyTo with overwrite flag to avoid FileSystemWatcher issues
            file.CopyTo(targetFilePath);
        }

        if (recursive)
        {
            foreach (var subDirectory in sourcePath.Folders())
            {
                var destDirectory = destPath.Combine(subDirectory);
                subDirectory.CopyTo(destDirectory, recursive);
            }
        }
    }
}