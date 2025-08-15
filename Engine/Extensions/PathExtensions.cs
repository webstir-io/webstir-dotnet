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

    public static string[] Files(this string path, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    public static string Name(this string path)
    {
        return Path.GetFileName(path);
    }

    public static bool Exists(this string path)
    {
        return Path.Exists(path);
    }

    public static async Task CopyToAsync(this string sourcePath, string destPath, bool recursive = true)
    {
        if (!sourcePath.Exists())
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

        var destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourcePath.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
            return;

        var fileTasks = new List<Task>();
        foreach (var file in sourcePath.Files())
        {
            var targetFilePath = destPath.Combine(file.Name());
            fileTasks.Add(CopyFileAsync(file, targetFilePath));
        }

        await Task.WhenAll(fileTasks);

        if (recursive)
        {
            var directoryTasks = new List<Task>();
            foreach (var subDirectory in sourcePath.Folders())
            {
                var destDirectory = destPath.Combine(subDirectory.Name());
                directoryTasks.Add(subDirectory.CopyToAsync(destDirectory, recursive));
            }

            await Task.WhenAll(directoryTasks);
        }
    }

    private static async Task CopyFileAsync(string sourceFile, string destFile)
    {
        using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await sourceStream.CopyToAsync(destStream);
    }
}