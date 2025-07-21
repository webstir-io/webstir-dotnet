using System.Diagnostics;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers.Client;

public class ScriptsWorker(App app) : IClientWorker
{
    public int BuildOrder => 1; // Heavy TypeScript compilation - let it use full CPU
    private const string _tsConfigBaseFile = "base.tsconfig.json";
    private const string _tsConfigClientFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _indexTsFile = "index.ts";
    private const string _refreshJsFile = "refresh.js";
    private const string _routerTsFile = "router.ts";
    private const string _navigationTsFile = "navigation.ts";

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        var baseTsConfigPath = app.WorkingDir.CombinePath(_tsConfigBaseFile);
        if (!File.Exists(baseTsConfigPath))
            AssemblyHelpers.WriteResourceToFile(_tsConfigBaseFile, baseTsConfigPath);

        string clientTsConfigPath = app.ClientDir.CombinePath(_tsConfigClientFile);
        if (!File.Exists(clientTsConfigPath))
            AssemblyHelpers.WriteResourceToFile("client", _tsConfigClientFile, clientTsConfigPath);

        string outputRefreshJsFilepath = app.ClientAppDir.CombinePath(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _refreshJsFile, outputRefreshJsFilepath);

        string outputAppTsFilepath = app.ClientAppDir.CombinePath(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = app.ClientIndexDir.CombinePath(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _indexTsFile, outputIndexTsFilepath);

        string routerFilePath = app.ClientAppDir.CombinePath(_routerTsFile);
        if (!File.Exists(routerFilePath))
            AssemblyHelpers.WriteResourceToFile("client", _routerTsFile, routerFilePath);

        string navigationFilePath = app.ClientAppDir.CombinePath(_navigationTsFile);
        if (!File.Exists(navigationFilePath))
            AssemblyHelpers.WriteResourceToFile("client", _navigationTsFile, navigationFilePath);
    }

    public void Build(bool releaseMode = false)
    {
        var packageJsonPath = app.WorkingDir.CombinePath(App.Files.PackageJson);
        if (File.Exists(packageJsonPath) && !app.NodeModulesDir.Exists)
        {
            RunNpmInstall();
        }

        CompileTypeScriptFiles();
        FlattenBuildOutput();

        string sourceRefreshJsApp = app.ClientAppDir.CombinePath(_refreshJsFile);
        string targetRefreshJs = app.ClientBuildDir.CombinePath(_refreshJsFile);

        if (File.Exists(sourceRefreshJsApp))
        {
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        }
        else
        {
            Console.WriteLine($"Warning: {_refreshJsFile} not found in {sourceRefreshJsApp}");
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(app.ClientDistDir.FullName);

        foreach (FileInfo jsFile in app.ClientBuildDir.GetFiles("*.js", SearchOption.AllDirectories))
        {
            // Skip refresh.js as it's only for development
            if (jsFile.Name.Equals(_refreshJsFile, StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath = Path.GetRelativePath(app.ClientBuildDir.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(app.ClientDistDir.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    private void CompileTypeScriptFiles()
    {
        var clientTsConfigPath = app.ClientDir.CombinePath(_tsConfigClientFile);

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
        var nestedClientDirectory = app.ClientBuildDir.CreateSubDirectory("client");
        if (nestedClientDirectory.Exists)
        {
            // Move all contents up one level
            foreach (var item in nestedClientDirectory.GetDirectories())
            {
                var targetPath = app.ClientBuildDir.CombinePath(item.Name);
                if (Directory.Exists(targetPath))
                {
                    // If target exists, we need to merge contents
                    foreach (var file in item.GetFiles("*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(item.FullName, file.FullName);
                        var targetFilePath = Path.Combine(targetPath, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
                        file.MoveTo(targetFilePath, overwrite: true);
                    }
                    item.Delete(recursive: true);
                }
                else
                {
                    item.MoveTo(targetPath);
                }
            }
            
            foreach (var file in nestedClientDirectory.GetFiles())
            {
                var targetPath = app.ClientBuildDir.CombinePath(file.Name);
                file.MoveTo(targetPath, overwrite: true);
            }
            
            nestedClientDirectory.Delete(recursive: true);
        }
        
        // Then flatten pages as before
        var pagesDirectory = app.ClientBuildDir.CreateSubDirectory("pages");
        if (!pagesDirectory.Exists)
            return;

        foreach (var pageDirectory in pagesDirectory.GetDirectories())
        {
            var pageName = pageDirectory.Name;
            DirectoryInfo targetDirectory;

            if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                targetDirectory = app.ClientBuildDir;
            }
            else
            {
                targetDirectory = app.ClientBuildDir.CreateSubDirectory(pageName);
            }

            foreach (var jsFile in pageDirectory.GetFiles("*.js"))
            {
                var targetPath = targetDirectory.CombinePath(jsFile.Name);
                jsFile.MoveTo(targetPath, overwrite: true);
            }

            foreach (var mapFile in pageDirectory.GetFiles("*.js.map"))
            {
                var targetPath = targetDirectory.CombinePath(mapFile.Name);
                mapFile.MoveTo(targetPath, overwrite: true);
            }
        }

        if (pagesDirectory.Exists)
        {
            pagesDirectory.Delete(recursive: true);
        }
    }

    private void RunNpmInstall()
    {
        // Check if package-lock.json exists to determine which npm command to use
        var packageLockPath = app.WorkingDir.CombinePath("package-lock.json");
        var npmCommand = File.Exists(packageLockPath) ? "ci" : "install";
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = app.WorkingDir.FullName
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

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var tsFilePath = pageDirectory.CombinePath($"{pageName}.ts");
        var tsContent = $"""
            import '../../app/app.js';

            console.log('{pageName} page loaded');
            """;
        File.WriteAllText(tsFilePath, tsContent);
    }

    public void AddPage(string name) 
    {
        var pageDirectory = app.ClientPagesDir.CreateSubDirectory(name);
        AddPage(pageDirectory);
    }
}