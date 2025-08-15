namespace Engine.Extensions;

public static class DirectoryExtensions
{
    public static string CreateSubDirectory(this string path, string subDirectory)
    {
        var fullPath = Path.Combine(path, subDirectory);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public static string CombinePath(this DirectoryInfo directoryInfo, params string[] paths)
    {
        var allPaths = new string[paths.Length + 1];
        allPaths[0] = directoryInfo.FullName;
        paths.CopyTo(allPaths, 1);
        return Path.Combine(allPaths);
    }

    public static async Task CopyToAsync(this DirectoryInfo sourceDirectory, string destPath, bool recursive = true)
    {
        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");

        var destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourceDirectory.FullName.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
            return;

        var fileTasks = new List<Task>();
        foreach (var file in sourceDirectory.GetFiles())
        {
            var targetFilePath = Path.Combine(destPath, file.Name);
            fileTasks.Add(CopyFileAsync(file.FullName, targetFilePath));
        }

        await Task.WhenAll(fileTasks);

        if (recursive)
        {
            var directoryTasks = new List<Task>();
            foreach (var subDirectory in sourceDirectory.GetDirectories())
            {
                var destDirectory = Path.Combine(destPath, subDirectory.Name);
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