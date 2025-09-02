using Engine.Extensions;
using Engine.Pipelines.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Engine.Pipelines.JavaScript.Build;

public class JsBuilder(AppWorkspace workspace, ILogger<JsBuilder> logger)
{
    private const string RefreshJsFile = "refresh.js";
    private const string BaseTsConfig = "base.tsconfig.json";

    public void Build(DiagnosticCollection? diagnostics = null)
    {
        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
            RunNpmInstall();

        CompileTypeScriptFiles(diagnostics);
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

    private void CompileTypeScriptFiles(DiagnosticCollection? diagnostics)
    {
        string baseTsConfigPath = workspace.WorkingPath.Combine(BaseTsConfig);
        try
        {
            RunProcess("tsc", $"--build \"{baseTsConfigPath}\"", "TypeScript compilation");
        }
        catch (Exception ex)
        {
            if (diagnostics != null)
            {
                ParseTscDiagnostics(ex.Message, diagnostics);
            }
            else
            {
                throw;
            }
        }
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

    private static void ParseTscDiagnostics(string text, DiagnosticCollection diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Support both classic and newer tsc formats:
        // 1) path.ts(10,5): error TS1234: Message
        // 2) path.ts:10:5 - error TS1234: Message
        System.Text.RegularExpressions.Regex classic = new(
            @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*error\s+TS\d+:\s*(?<msg>.+)$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        System.Text.RegularExpressions.Regex modern = new(
            @"^(?<file>.+?):(?<line>\d+):(?<col>\d+)\s*-\s*error\s+TS\d+:\s*(?<msg>.+)$",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        int added = 0;
        foreach (System.Text.RegularExpressions.Match match in classic.Matches(text))
        {
            string file = match.Groups["file"].Value.Trim();
            int line = int.TryParse(match.Groups["line"].Value, out int ln) ? ln : 0;
            int col = int.TryParse(match.Groups["col"].Value, out int cl) ? cl : 0;
            string message = match.Groups["msg"].Value.Trim();
            diagnostics.AddError(message, file, line, col);
            added++;
        }
        foreach (System.Text.RegularExpressions.Match match in modern.Matches(text))
        {
            string file = match.Groups["file"].Value.Trim();
            int line = int.TryParse(match.Groups["line"].Value, out int ln) ? ln : 0;
            int col = int.TryParse(match.Groups["col"].Value, out int cl) ? cl : 0;
            string message = match.Groups["msg"].Value.Trim();
            diagnostics.AddError(message, file, line, col);
            added++;
        }

        if (added == 0)
        {
            // Fall back to a single error if we couldn't parse specifics
            diagnostics.AddError("TypeScript compilation failed", null, null, null);
        }
    }
}
