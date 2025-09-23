using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Microsoft.Extensions.Options;

namespace Engine.Bridge.Backend;

public class BackendWorker(AppWorkspace workspace, IOptions<AppSettings> options) : IWorkflowWorker
{
    private readonly AppSettings _settings = options.Value;
    private const string _tsConfigFile = "tsconfig.json";

    public int BuildOrder => 2;

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.BackendPath, workspace.BackendPath);

    public Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Backend))
        {
            return Task.CompletedTask;
        }

        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
        {
            NpmHelper.RunNpmInstall(workspace.WorkingPath);
        }

        CompileTypeScriptFiles();

        return Task.CompletedTask;
    }

    public Task PublishAsync()
    {
        foreach (string jsFilepath in Directory.GetFiles(workspace.BackendBuildPath, "*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(workspace.BackendBuildPath, jsFilepath);
            string targetFilePath = Path.Combine(workspace.BackendDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFilepath);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }
        return Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        string tsConfigPath = workspace.BackendPath.Combine(_tsConfigFile);

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

}
