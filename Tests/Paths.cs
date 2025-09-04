using System;
using System.IO;

namespace Tests;

public static class Paths
{
    private const string OutFolderName = "out";

    private static string TestsProjectRoot
    {
        get
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            return projectDir;
        }
    }

    public static string OutPath
    {
        get
        {
            string path = Path.Combine(TestsProjectRoot, OutFolderName);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
