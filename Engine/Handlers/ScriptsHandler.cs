using Engine.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Engine.Handlers;

public partial class ScriptsHandler(AppWorkspace workspace, ILogger<ScriptsHandler> logger)
{
    private const string RefreshJsFile = "refresh.js";
    private const string BaseTsConfig = "base.tsconfig.json";
    
    [GeneratedRegex(@"\.\d{10}$")]
    private static partial Regex TimestampPattern();
    
    [GeneratedRegex(@"(?<!:)//.*$", RegexOptions.Multiline)]
    private static partial Regex SingleLineCommentPattern();
    
    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex MultiLineCommentPattern();
    
    [GeneratedRegex(@"^\s*\r?\n", RegexOptions.Multiline)]
    private static partial Regex EmptyLinePattern();

    public async Task BuildAsync()
    {
        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
            RunNpmInstall();

        CompileTypeScriptFiles();

        // JS files are already in place after TypeScript compilation

        string sourceRefreshJsApp = workspace.ClientAppPath.Combine(RefreshJsFile);
        string targetRefreshJs = workspace.ClientBuildPath.Combine(RefreshJsFile);

        if (sourceRefreshJsApp.Exists())
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        else
            logger.LogWarning("{RefreshJsFile} not found in {SourcePath}", RefreshJsFile, sourceRefreshJsApp);

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        string[] jsFiles = workspace.ClientBuildPath.Files("*.js", SearchOption.AllDirectories)
            .Where(f => !f.Filename().Equals(RefreshJsFile, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string jsFile in jsFiles)
        {
            string relativePath = Path.GetRelativePath(workspace.ClientBuildPath, jsFile);
            string targetFilePath = workspace.ClientDistPath.Combine(relativePath);
            targetFilePath.DirectoryName().Create();

            string jsContent = File.ReadAllText(jsFile);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        await Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        string baseTsConfigPath = workspace.WorkingPath.Combine(BaseTsConfig);

        ProcessStartInfo processInfo = new()
        {
            FileName = "tsc",
            Arguments = $"--build \"{baseTsConfigPath}\"",
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
            string errorMessage = $"TypeScript compilation failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            throw new Exception(errorMessage);
        }
    }


    private void RunNpmInstall()
    {
        string packageLockPath = workspace.WorkingPath.Combine(Files.PackageLockJson);
        string npmCommand = packageLockPath.Exists() ? "ci" : "install";
        
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

        using var process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start npm install process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = $"npm install failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"\nErrors:\n{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"\nOutput:\n{output}";
            throw new Exception(errorMessage);
        }
    }

    private static string RemoveJavaScriptComments(string js)
    {
        js = SingleLineCommentPattern().Replace(js, string.Empty);
        js = MultiLineCommentPattern().Replace(js, string.Empty);
        js = EmptyLinePattern().Replace(js, string.Empty);
        return js.Trim();
    }

    public async Task AddPageAsync(string pageName)
    {
        string pageDirectory = workspace.ClientPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            import '../../{Folders.App}/workspace.js';

            console.log('{pageName} page loaded');
            """;
        File.WriteAllText(tsFilePath, tsContent);
        await Task.CompletedTask;
    }
}