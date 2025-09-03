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

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.ServerPath, workspace.ServerPath);

    public Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Server))
        {
            return Task.CompletedTask;
        }

        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
        {
            RunNpmInstall();
        }

        CompileTypeScriptFiles();

        return Task.CompletedTask;
    }

    public Task PublishAsync()
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
        return Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        string tsConfigPath = workspace.ServerPath.Combine(_tsConfigFile);

        ProcessStartInfo processInfo = new()
        {
            FileName = "tsc",
            Arguments = $"-p \"{tsConfigPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processInfo.Environment["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        processInfo.Environment["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        using Process process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start TypeScript compiler process for server.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = $"Server TypeScript compilation failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
            {
                errorMessage += $"\nErrors:\n{errors}";
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                errorMessage += $"\nOutput:\n{output}";
            }
            throw new Exception(errorMessage);
        }
    }

    private void RunNpmInstall()
    {
        string packageLockPath = workspace.WorkingPath.Combine("package-lock.json");
        string npmCommand = File.Exists(packageLockPath) ? "ci" : "install";

        ProcessStartInfo processInfo = new()
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workspace.WorkingPath
        };

        using Process process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start npm install process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = $"npm install failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
            {
                errorMessage += $"\nErrors:\n{errors}";
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                errorMessage += $"\nOutput:\n{output}";
            }
            throw new Exception(errorMessage);
        }
    }

    private static string RemoveJavaScriptComments(string js)
    {
        string singleLinePattern = @"(?<!:)//.*$";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            singleLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        string multiLinePattern = @"/\*[\s\S]*?\*/";
        js = System.Text.RegularExpressions.Regex.Replace(js, multiLinePattern, string.Empty);

        string emptyLinePattern = @"^\s*\r?\n";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            emptyLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        return js.Trim();
    }

    public Task AddPageAsync(string pageName) => Task.CompletedTask;
}
