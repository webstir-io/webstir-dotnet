using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Engine.Extensions;

public static class PathExtensions
{
    public static string CreateSubDirectory(this string path, string subDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(subDirectory);
        return path.Combine(subDirectory).Create();
    }

    public static string Combine(this string path, params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(segments);
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    public static string Create(this string path) => Directory.CreateDirectory(path).FullName;

    public static string[] Folders(this string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        return Directory.GetDirectories(path);
    }

    public static string[] Files(this string path, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    public static string Filename(this string path) => Path.GetFileName(path);

    public static string DirectoryName(this string path) => Path.GetDirectoryName(path) ?? string.Empty;

    public static bool Exists(this string path) => Path.Exists(path);

    public static async Task CopyToAsync(this string sourcePath, string destPath, bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destPath);

        if (!sourcePath.Exists())
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        DirectoryInfo destDir = Directory.CreateDirectory(destPath);

        // Skip if source and destination are the same to prevent infinite loops
        if (sourcePath.Equals(destDir.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<Task> fileTasks = [];
        foreach (string file in sourcePath.Files())
        {
            string targetFilePath = destPath.Combine(file.Filename());
            fileTasks.Add(CopyFileAsync(file, targetFilePath));
        }

        await Task.WhenAll(fileTasks);

        if (recursive)
        {
            List<Task> directoryTasks = [];
            foreach (string subDirectory in sourcePath.Folders())
            {
                string destDirectory = destPath.Combine(subDirectory.Filename());
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
