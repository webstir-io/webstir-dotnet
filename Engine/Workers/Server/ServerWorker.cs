using System.Diagnostics;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workers;

public class ServerWorker() : IFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder { get; } = 1; // Run parallel with other workers

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        // Skip server files for ClientOnly mode
        if (mode == ProjectMode.ClientOnly)
            return;

        // For init, we want to create the directory
        string tsConfigPath = Directories.ServerDirectory.Join(_tsConfigFile);
        if (!File.Exists(tsConfigPath))
            AssemblyHelpers.WriteResourceToFile(Settings.ServerFolder, _tsConfigFile, tsConfigPath);

        string indexTsPath = Directories.ServerDirectory.Join(_indexTsFile);
        if (!File.Exists(indexTsPath))
            AssemblyHelpers.WriteResourceToFile(Settings.ServerFolder, _indexTsFile, indexTsPath);
    }

    public void Build(bool releaseMode = false)
    {
        if (!Directories.GetServerDirectory().Exists)
            return;

        // Check if node_modules exists and package.json exists
        var packageJsonPath = Path.Combine(Settings.WorkingDirectory, Settings.PackageJsonFile);
        if (File.Exists(packageJsonPath) && !Directories.NodeModulesDirectory.Exists)
        {
            RunNpmInstall();
        }

        CompileTypeScriptFiles();
    }

    public void Publish()
    {
        if (!Directories.ServerDirectory.Exists)
            return;

        Directory.CreateDirectory(Directories.ServerDistDirectory.FullName);

        // Copy all .js files from server build to server dist
        foreach (FileInfo jsFile in Directories.ServerBuildDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(Directories.ServerBuildDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(Directories.ServerDistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            
            // Read JS content and remove comments
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    private static void CompileTypeScriptFiles()
    {
        var tsConfigPath = Directories.ServerDirectory.Join(_tsConfigFile);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "tsc",
            Arguments = $"-p \"{tsConfigPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start TypeScript compiler process for server.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            var errorMessage = $"Server TypeScript compilation failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            throw new Exception(errorMessage);
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