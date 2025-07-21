using System.Diagnostics;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers.Server;

public class ServerWorker(App app) : IModuleWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder => 3; // Depends on SharedWorker, can run with other fast operations

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        string tsConfigPath = app.ServerDir.CombinePath(_tsConfigFile);
        if (!File.Exists(tsConfigPath))
            AssemblyHelpers.WriteResourceToFile(App.Folders.Server, _tsConfigFile, tsConfigPath);

        string indexTsPath = app.ServerDir.CombinePath(_indexTsFile);
        if (!File.Exists(indexTsPath))
            AssemblyHelpers.WriteResourceToFile(App.Folders.Server, _indexTsFile, indexTsPath);
    }

    public void Build(bool releaseMode = false)
    {
        if (!app.ServerDir.Exists)
            return;

        // Check if node_modules exists and package.json exists
        var packageJsonPath = app.WorkingDir.CombinePath(App.Files.PackageJson);
        if (File.Exists(packageJsonPath) && !app.NodeModulesDir.Exists)
            RunNpmInstall();

        CompileTypeScriptFiles();
    }

    public void Publish()
    {
        if (!app.ServerDir.Exists)
            return;

        Directory.CreateDirectory(app.ServerDistDir.FullName);

        // Copy all .js files from server build to server dist
        foreach (FileInfo jsFile in app.ServerBuildDir.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(app.ServerBuildDir.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(app.ServerDistDir.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            
            // Read JS content and remove comments
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    private void CompileTypeScriptFiles()
    {
        var tsConfigPath = app.ServerDir.CombinePath(_tsConfigFile);

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

    public void AddPage(DirectoryInfo pageDirectory) { }
}