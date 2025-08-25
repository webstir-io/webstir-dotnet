using System.Diagnostics;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Microsoft.Extensions.Options;

namespace Engine.Workers;

public class ServerWorker(AppWorkspace workspace, IOptions<AppSettings> options) : IWorker
{
    private readonly AppSettings _settings = options.Value;
    private const string _tsConfigFile = "tsconfig.json";

    public int BuildOrder => 2;

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    {
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.ServerPath, workspace.ServerPath);
    }

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Server))
            return;

        var packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
            RunNpmInstall();

        CompileTypeScriptFiles();

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        foreach (string jsFilepath in Directory.GetFiles(workspace.ServerBuildPath, "*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(workspace.ServerBuildPath, jsFilepath);
            string targetFilePath = Path.Combine(workspace.ServerDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFilepath);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        await Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        var tsConfigPath = workspace.ServerPath.Combine(_tsConfigFile);

        var processInfo = new ProcessStartInfo
        {
            FileName = "tsc",
            Arguments = $"-p \"{tsConfigPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        processInfo.Environment["API_PORT"] = _settings.ApiServerPort.ToString();
        processInfo.Environment["WEB_PORT"] = _settings.WebServerPort.ToString();

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
        var packageLockPath = workspace.WorkingPath.Combine("package-lock.json");
        var npmCommand = File.Exists(packageLockPath) ? "ci" : "install";
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workspace.WorkingPath
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

    public async Task AddPageAsync(string pageName)
    {
        await Task.CompletedTask;
    }
}