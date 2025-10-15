using System;
using System.IO;

namespace Tester.Infrastructure;

public static class Paths
{
    private const string OutFolderName = "out";

    private static string ProjectRoot
    {
        get
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        }
    }

    public static string OutPath => EnsurePath(Path.Combine(ProjectRoot, OutFolderName));

    public static string RepositoryRoot
    {
        get
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(ProjectRoot, ".."));
            return repositoryRoot;
        }
    }

    private static string EnsurePath(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
