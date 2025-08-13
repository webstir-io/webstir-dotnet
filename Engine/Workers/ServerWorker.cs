using System.Diagnostics;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers.Server;

public class ServerWorker(AppContext context) : IWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder => 2; // Fast server compilation

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    {
        string tsConfigPath = context.ServerPath.Combine(_tsConfigFile);
        if (!File.Exists(tsConfigPath))
            AssemblyHelpers.WriteResourceToFile(Folders.Server, _tsConfigFile, tsConfigPath);

        string indexTsPath = context.ServerPath.Combine(_indexTsFile);
        if (!File.Exists(indexTsPath))
            AssemblyHelpers.WriteResourceToFile(Folders.Server, _indexTsFile, indexTsPath);

        await Task.CompletedTask;
    }

    public async Task BuildAsync(bool releaseMode = false)
    {
        // Check if node_modules exists and package.json exists
        var packageJsonPath = context.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
            RunNpmInstall();

        CompileTypeScriptFiles();

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        foreach (string jsFilepath in Directory.GetFiles(context.ServerBuildPath, "*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(context.ServerBuildPath, jsFilepath);
            string targetFilePath = Path.Combine(context.ServerDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFilepath);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        await Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        var tsConfigPath = context.ServerPath.Combine(_tsConfigFile);

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

    public async Task AddPageAsync(string pageName)
    {
        await Task.CompletedTask;
    }
}