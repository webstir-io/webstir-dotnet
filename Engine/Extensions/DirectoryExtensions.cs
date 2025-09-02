namespace Engine.Extensions;

public static class DirectoryExtensions
{
    public static string CreateSubDirectory(this string path, string subDirectory)
    {
        string fullPath = Path.Combine(path, subDirectory);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public static string CombinePath(this DirectoryInfo directoryInfo, params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(directoryInfo);
        ArgumentNullException.ThrowIfNull(paths);

        string[] allPaths = new string[paths.Length + 1];
        allPaths[0] = directoryInfo.FullName;
        paths.CopyTo(allPaths, 1);
        return Path.Combine(allPaths);
    }

    public static async Task CopyToAsync(this DirectoryInfo sourceDirectory, string destPath, bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(destPath);

        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");
        }

        DirectoryInfo destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourceDirectory.FullName.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<Task> fileTasks = [];
        foreach (FileInfo file in sourceDirectory.GetFiles())
        {
            string targetFilePath = Path.Combine(destPath, file.Name);
            fileTasks.Add(CopyFileAsync(file.FullName, targetFilePath));
        }

        await Task.WhenAll(fileTasks);

        if (recursive)
        {
            List<Task> directoryTasks = [];
            foreach (DirectoryInfo subDirectory in sourceDirectory.GetDirectories())
            {
                string destDirectory = Path.Combine(destPath, subDirectory.Name);
                directoryTasks.Add(subDirectory.CopyToAsync(destDirectory, recursive));
            }

            await Task.WhenAll(directoryTasks);
        }
    }

    private static async Task CopyFileAsync(string sourceFile, string destFile)
    {
        using FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using FileStream destStream = new(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await sourceStream.CopyToAsync(destStream);
    }
}
