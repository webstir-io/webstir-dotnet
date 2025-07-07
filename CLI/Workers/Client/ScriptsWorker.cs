using System.Diagnostics;
using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;

namespace CLI.Workers;

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
        // Always create base TypeScript config
        if (!File.Exists(_tsConfigBaseFile))
            AssemblyHelpers.WriteResourceToFile(_tsConfigBaseFile, _tsConfigBaseFile);

        // Skip client files for ServerOnly mode
        if (mode == ProjectMode.ServerOnly)
            return;

        // Always create client TypeScript config
        string clientTsConfigPath = Directories.ClientDirectory.Join(_tsConfigClientFile);
        if (!File.Exists(clientTsConfigPath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _tsConfigClientFile, clientTsConfigPath);

        string outputRefreshJsFilepath = Directories.ClientAppDirectory.Join(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _refreshJsFile, outputRefreshJsFilepath);

        // Always create TypeScript files
        string outputAppTsFilepath = Directories.ClientAppDirectory.Join(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = Directories.ClientIndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _indexTsFile, outputIndexTsFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        // Check if node_modules exists and package.json exists
        var packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), Settings.PackageJsonFile);
        if (File.Exists(packageJsonPath) && !Directories.NodeModulesDirectory.Exists)
        {
            Console.WriteLine("Installing npm dependencies...");
            RunNpmInstall();
        }

        CompileTypeScriptFiles();

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
        Directory.CreateDirectory(Directories.ClientDistDirectory.FullName); // Ensure dist directory exists

        // Copy all .js files from BuildDirectory to DistDirectory, maintaining subfolder structure
        foreach (FileInfo jsFile in Directories.ClientBuildDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(Directories.ClientBuildDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(Directories.ClientDistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!); // Ensure sub-directory exists in target
            
            // Read JS content and remove comments
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var tsFilePath = pageDirectory.Join($"{pageName}.ts");
        File.WriteAllText(tsFilePath, $"// TypeScript file for {pageName} page\n");
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

    private static void RunNpmInstall()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = "ci",  // Use ci for reproducible installs
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
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
        // Remove single-line comments (// ...) but preserve URLs
        var singleLinePattern = @"(?<!:)//.*$";
        js = System.Text.RegularExpressions.Regex.Replace(
            js, 
            singleLinePattern, 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Remove multi-line comments (/* ... */)
        var multiLinePattern = @"/\*[\s\S]*?\*/";
        js = System.Text.RegularExpressions.Regex.Replace(js, multiLinePattern, string.Empty);
        
        // Remove empty lines left by comment removal
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