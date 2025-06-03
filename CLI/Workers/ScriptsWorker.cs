using System.Diagnostics;
using CLI.Helpers;
using System.IO; // Added for Path and Directory operations

namespace CLI.Workers;

public class ScriptsWorker() : IWebFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _appJsFile = "app.js";
    private const string _indexTsFile = "index.ts";
    private const string _refreshJsFile = "refresh.js";

    public int BuildOrder { get; } = 1;

    public void Init()
    {
        if (!File.Exists(_tsConfigFile))
            AssemblyHelpers.WriteResourceToFile(_tsConfigFile, _tsConfigFile);

        string outputRefreshJsFilepath = Directories.AppDirectory.Join(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile(_refreshJsFile, outputRefreshJsFilepath);

        string outputAppTsFilepath = Directories.AppDirectory.Join(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = Directories.IndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputIndexTsFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        ComplileTypeScriptFiles();

        // Copy compiled JS files from build to bin
        CopyCompiledJsFiles(Directories.BuildDirectory, Directories.BinDirectory);

        if (!releaseMode)
        {
            string sourceRefreshJsApp = Directories.AppDirectory.Join(_refreshJsFile);
            string sourceRefreshJsBuild = Path.Combine(Directories.BuildDirectory.FullName, Settings.AppFolder, _refreshJsFile); // Corrected path construction
            string targetRefreshJs = Directories.BinDirectory.Join(_refreshJsFile);

            if (File.Exists(sourceRefreshJsApp))
            {
                 File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
            }
            else if (File.Exists(sourceRefreshJsBuild))
            {
                 File.Copy(sourceRefreshJsBuild, targetRefreshJs, true);
            }
            else
            {
                Console.WriteLine($"Warning: {_refreshJsFile} not found in {Directories.AppDirectory.FullName} or {sourceRefreshJsBuild}");
            }
        }
    }

    private static void CopyCompiledJsFiles(DirectoryInfo sourceDir, DirectoryInfo targetDir)
    {
        if (!sourceDir.Exists)
        {
            Console.WriteLine($"Warning: Source directory for JS compilation output not found: {sourceDir.FullName}");
            return;
        }

        // Ensure the target directory exists
        Directory.CreateDirectory(targetDir.FullName);

        // Copy all .js files, maintaining subfolder structure
        foreach (FileInfo jsFile in sourceDir.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(targetDir.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!); // Ensure sub-directory exists in target
            File.Copy(jsFile.FullName, targetFilePath, true);
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(Directories.DistDirectory.FullName); // Ensure dist directory exists

        // Copy all .js files from BinDirectory to DistDirectory, maintaining subfolder structure
        foreach (FileInfo jsFile in Directories.BinDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(Directories.BinDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(Directories.DistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!); // Ensure sub-directory exists in target
            File.Copy(jsFile.FullName, targetFilePath, true);
            // Console.WriteLine($"ScriptsWorker: Published {jsFile.FullName} to {targetFilePath}"); // Optional: for debugging
        }
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        File.Create(pageDirectory.Join($"{pageName}.ts")).Close();
    }

    private static void ComplileTypeScriptFiles()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "tsc",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start TypeScript compiler process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            throw new Exception($"TypeScript compilation failed. Exit Code: {process.ExitCode}\nOutput:\n{output}\nErrors:\n{errors}");
        }
    }
}