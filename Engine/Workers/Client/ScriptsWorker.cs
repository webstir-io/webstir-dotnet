using System.Diagnostics;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workers.Client;

public class ScriptsWorker() : IPageWorker
{
    private const string _tsConfigBaseFile = "base.tsconfig.json";
    private const string _tsConfigClientFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _indexTsFile = "index.ts";
    private const string _refreshJsFile = "refresh.js";

    public int BuildOrder { get; } = 1;

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        var baseTsConfigPath = Path.Combine(Settings.WorkingDirectory, _tsConfigBaseFile);
        if (!File.Exists(baseTsConfigPath))
            AssemblyHelpers.WriteResourceToFile(_tsConfigBaseFile, baseTsConfigPath);

        if (mode == ProjectMode.ServerOnly)
            return;

        string clientTsConfigPath = Directories.ClientDirectory.Join(_tsConfigClientFile);
        if (!File.Exists(clientTsConfigPath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _tsConfigClientFile, clientTsConfigPath);

        string outputRefreshJsFilepath = Directories.ClientAppDirectory.Join(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _refreshJsFile, outputRefreshJsFilepath);

        string outputAppTsFilepath = Directories.ClientAppDirectory.Join(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = Directories.ClientIndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _indexTsFile, outputIndexTsFilepath);
            
        string routerFilePath = Directories.ClientAppDirectory.Join("router.ts");
        if (!File.Exists(routerFilePath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, "router.ts", routerFilePath);
            
        string navigationFilePath = Directories.ClientAppDirectory.Join("navigation.ts");
        if (!File.Exists(navigationFilePath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, "navigation.ts", navigationFilePath);
    }

    public void Build(bool releaseMode = false)
    {
        var packageJsonPath = Path.Combine(Settings.WorkingDirectory, Settings.PackageJsonFile);
        if (File.Exists(packageJsonPath) && !Directories.NodeModulesDirectory.Exists)
        {
            RunNpmInstall();
        }

        CompileTypeScriptFiles();
        FlattenBuildOutput();

        if (!releaseMode)
        {
            string sourceRefreshJsApp = Directories.ClientAppDirectory.Join(_refreshJsFile);
            string targetRefreshJs = Directories.ClientBuildDirectory.Join(_refreshJsFile);

            if (File.Exists(sourceRefreshJsApp))
            {
                File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
            }
            else
            {
                Console.WriteLine($"Warning: {_refreshJsFile} not found in {sourceRefreshJsApp}");
            }
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(Directories.ClientDistDirectory.FullName);

        foreach (FileInfo jsFile in Directories.ClientBuildDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            // Skip refresh.js as it's only for development
            if (jsFile.Name.Equals(_refreshJsFile, StringComparison.OrdinalIgnoreCase))
                continue;
                
            string relativePath = Path.GetRelativePath(Directories.ClientBuildDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(Directories.ClientDistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var tsFilePath = pageDirectory.Join($"{pageName}.ts");
        var tsContent = $"""
            import '../../app/app.js';

            console.log('{pageName} page loaded');
            """;
        File.WriteAllText(tsFilePath, tsContent);
    }

    private static void CompileTypeScriptFiles()
    {
        var clientTsConfigPath = Directories.ClientDirectory.Join(_tsConfigClientFile);
        
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

    private static void FlattenBuildOutput()
    {
        // First, move everything from client/client/* up one level
        var nestedClientDirectory = Directories.ClientBuildDirectory.SubDirectory("client");
        if (nestedClientDirectory.Exists)
        {
            // Move all contents up one level
            foreach (var item in nestedClientDirectory.GetDirectories())
            {
                var targetPath = Directories.ClientBuildDirectory.Join(item.Name);
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
                var targetPath = Directories.ClientBuildDirectory.Join(file.Name);
                file.MoveTo(targetPath, overwrite: true);
            }
            
            nestedClientDirectory.Delete(recursive: true);
        }
        
        // Then flatten pages as before
        var pagesDirectory = Directories.ClientBuildDirectory.SubDirectory("pages");
        if (!pagesDirectory.Exists)
            return;

        foreach (var pageDirectory in pagesDirectory.GetDirectories())
        {
            var pageName = pageDirectory.Name;
            DirectoryInfo targetDirectory;

            if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                targetDirectory = Directories.ClientBuildDirectory;
            }
            else
            {
                targetDirectory = Directories.ClientBuildDirectory.SubDirectory(pageName);
            }

            foreach (var jsFile in pageDirectory.GetFiles("*.js"))
            {
                var targetPath = targetDirectory.Join(jsFile.Name);
                jsFile.MoveTo(targetPath, overwrite: true);
            }

            foreach (var mapFile in pageDirectory.GetFiles("*.js.map"))
            {
                var targetPath = targetDirectory.Join(mapFile.Name);
                mapFile.MoveTo(targetPath, overwrite: true);
            }
        }

        if (pagesDirectory.Exists)
        {
            pagesDirectory.Delete(recursive: true);
        }
    }

    private static void RunNpmInstall()
    {
        // Check if package-lock.json exists to determine which npm command to use
        var packageLockPath = Path.Combine(Settings.WorkingDirectory, "package-lock.json");
        var npmCommand = File.Exists(packageLockPath) ? "ci" : "install";
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Settings.WorkingDirectory
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
}