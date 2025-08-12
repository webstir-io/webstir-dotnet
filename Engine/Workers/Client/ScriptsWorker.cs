using System.Diagnostics;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers.Client;

public class ScriptsWorker(AppContext context) : IClientWorker
{
    public int BuildOrder => 1;
    private const string _tsConfigBaseFile = "base.tsconfig.json";
    private const string _tsConfigClientFile = "tsconfig.json";
    private const string _appTsFile = "context.ts";
    private const string _indexTsFile = "index.ts";
    private const string _refreshJsFile = "refresh.js";
    private const string _routerTsFile = "router.ts";
    private const string _navigationTsFile = "navigation.ts";

    public async Task Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        var baseTsConfigPath = context.WorkingPath.Combine(_tsConfigBaseFile);
        if (!File.Exists(baseTsConfigPath))
            AssemblyHelpers.WriteResourceToFile(_tsConfigBaseFile, baseTsConfigPath);

        string clientTsConfigPath = context.ClientPath.Combine(_tsConfigClientFile);
        if (!File.Exists(clientTsConfigPath))
            AssemblyHelpers.WriteResourceToFile("client", _tsConfigClientFile, clientTsConfigPath);

        string outputRefreshJsFilepath = context.ClientAppPath.Combine(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _refreshJsFile, outputRefreshJsFilepath);

        string outputAppTsFilepath = context.ClientAppPath.Combine(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = context.ClientPagesPath.Combine("home", _indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _indexTsFile, outputIndexTsFilepath);

        string routerFilePath = context.ClientAppPath.Combine(_routerTsFile);
        if (!File.Exists(routerFilePath))
            AssemblyHelpers.WriteResourceToFile("client", _routerTsFile, routerFilePath);

        string navigationFilePath = context.ClientAppPath.Combine(_navigationTsFile);
        if (!File.Exists(navigationFilePath))
            AssemblyHelpers.WriteResourceToFile("client", _navigationTsFile, navigationFilePath);

        await Task.CompletedTask;
    }

    public async Task Build(bool releaseMode = false)
    {
        var packageJsonPath = context.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
            RunNpmInstall();

        CompileTypeScriptFiles();
        FlattenBuildOutput();

        string sourceRefreshJsApp = context.ClientAppPath.Combine(_refreshJsFile);
        string targetRefreshJs = context.ClientBuildPath.Combine(_refreshJsFile);

        if (File.Exists(sourceRefreshJsApp))
        {
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        }
        else
        {
            Console.WriteLine($"Warning: {_refreshJsFile} not found in {sourceRefreshJsApp}");
        }

        await Task.CompletedTask;
    }

    public async Task Publish()
    {
        foreach (string jsFile in Directory.GetFiles(context.ClientBuildPath, "*.js", SearchOption.AllDirectories))
        {
            // Skip refresh.js as it's only for development
            if (Path.GetFileName(jsFile).Equals(_refreshJsFile, StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath = Path.GetRelativePath(context.ClientBuildPath, jsFile);
            string targetFilePath = Path.Combine(context.ClientDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFile);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        await Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        var clientTsConfigPath = context.ClientPath.Combine(_tsConfigClientFile);

        var processInfo = new ProcessStartInfo
        {
            FileName = "tsc",
            Arguments = $"-p \"{clientTsConfigPath}\"",
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
            var errorMessage = $"TypeScript compilation failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            throw new Exception(errorMessage);
        }
    }

    private void FlattenBuildOutput()
    {
        // First, move everything from client/client/* up one level
        var nestedClientDirectory = context.ClientBuildPath.CreateSubDirectory("client");
        if (Directory.Exists(nestedClientDirectory))
        {
            // Move all contents up one level
            foreach (var item in Directory.GetDirectories(nestedClientDirectory))
            {
                var targetPath = context.ClientBuildPath.Combine(Path.GetFileName(item));
                if (Directory.Exists(targetPath))
                {
                    // If target exists, we need to merge contents
                    foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(item, file);
                        var targetFilePath = Path.Combine(targetPath, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
                        File.Move(file, targetFilePath, overwrite: true);
                    }
                    Directory.Delete(item, recursive: true);
                }
                else
                {
                    Directory.Move(item, targetPath);
                }
            }

            foreach (var file in Directory.GetFiles(nestedClientDirectory))
            {
                var targetPath = context.ClientBuildPath.Combine(Path.GetFileName(file));
                File.Move(file, targetPath, overwrite: true);
            }

            Directory.Delete(nestedClientDirectory, recursive: true);
        }
        
        // Then flatten pages as before
        var pagesDirectory = context.ClientBuildPath.CreateSubDirectory("pages");

        foreach (var pageDirectory in Directory.GetDirectories(pagesDirectory))
        {
            string pageName = Path.GetFileName(pageDirectory);
            string targetDirectory;

            if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                targetDirectory = context.ClientBuildPath;
            }
            else
            {
                targetDirectory = context.ClientBuildPath.CreateSubDirectory(pageName);
            }

            foreach (var jsFile in Directory.GetFiles(pageDirectory, "*.js"))
            {
                var targetPath = targetDirectory.Combine(jsFile);
                File.Move(jsFile, targetPath, overwrite: true);
            }

            foreach (var mapFile in Directory.GetFiles(pageDirectory, "*.js.map"))
            {
                var targetPath = targetDirectory.Combine(mapFile);
                File.Move(mapFile, targetPath, overwrite: true);
            }
        }

        Directory.Delete(pagesDirectory, recursive: true);
    }

    private void RunNpmInstall()
    {
        // Check if package-lock.json exists to determine which npm command to use
        var packageLockPath = context.WorkingPath.Combine("package-lock.json");
        var npmCommand = File.Exists(packageLockPath) ? "ci" : "install";
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = context.WorkingPath
        };

        using var process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start npm install process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            var errorMessage = $"npm install failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            throw new Exception(errorMessage);
        }
    }

    private static string RemoveJavaScriptComments(string js)
    {
        var singleLinePattern = @"(?<!:)//.*$";
        js = System.Text.RegularExpressions.Regex.Replace(
            js, 
            singleLinePattern, 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        var multiLinePattern = @"/\*[\s\S]*?\*/";
        js = System.Text.RegularExpressions.Regex.Replace(js, multiLinePattern, string.Empty);
        
        var emptyLinePattern = @"^\s*\r?\n";
        js = System.Text.RegularExpressions.Regex.Replace(
            js, 
            emptyLinePattern, 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        return js.Trim();
    }

    public async Task AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var tsFilePath = pageDirectory.CombinePath($"{pageName}.ts");
        var tsContent = $"""
            import '../../app/context.js';

            console.log('{pageName} page loaded');
            """;
        File.WriteAllText(tsFilePath, tsContent);

        await Task.CompletedTask;
    }

    public async Task AddPage(string name) 
    {
        var pageDirectory = context.ClientPagesPath.CreateSubDirectory(name);
        await AddPage(pageDirectory);
    }
}