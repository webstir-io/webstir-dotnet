using Engine.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Engine.Pipelines.JavaScript.Build;

public class JsBuilder(AppWorkspace workspace, ILogger<JsBuilder> logger)
{
    private const string RefreshJsFile = "refresh.js";
    private const string BaseTsConfig = "base.tsconfig.json";

    public void Build()
    {
        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
            RunNpmInstall();

        CompileTypeScriptFiles();
        CopyRefreshScript();
    }

    private void CopyRefreshScript()
    {
        string sourceRefreshJsApp = workspace.ClientAppPath.Combine(RefreshJsFile);
        string targetRefreshJs = workspace.ClientBuildPath.Combine(RefreshJsFile);

        if (sourceRefreshJsApp.Exists())
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        else
            logger.LogWarning("{RefreshJsFile} not found in {SourcePath}", RefreshJsFile, sourceRefreshJsApp);
    }

    private void CompileTypeScriptFiles()
    {
        string baseTsConfigPath = workspace.WorkingPath.Combine(BaseTsConfig);
        RunProcess("tsc", $"--build \"{baseTsConfigPath}\"", "TypeScript compilation");
    }

    private void RunNpmInstall()
    {
        string packageLockPath = workspace.WorkingPath.Combine(Files.PackageLockJson);
        string npmCommand = packageLockPath.Exists() ? "ci" : "install";
        RunProcess("npm", npmCommand, "npm install", workspace.WorkingPath);
    }

    private static void RunProcess(string fileName, string arguments, string description, string? workingDirectory = null)
    {
        ProcessStartInfo processInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        using Process process = Process.Start(processInfo) 
            ?? throw new Exception($"Failed to start {description} process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = $"{description} failed (Exit Code: {process.ExitCode})";
            
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            
            throw new Exception(errorMessage);
        }
    }
}