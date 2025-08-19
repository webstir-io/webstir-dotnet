using System.Diagnostics;
using Engine.Extensions;

namespace Engine.Handlers;

public class ScriptsHandler(AppWorkspace workspace)
{
    private const string _refreshJsFile = "refresh.js";

    public async Task BuildAsync()
    {
        var packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
            RunNpmInstall();

        CompileTypeScriptFiles();

        string sourceRefreshJsApp = workspace.ClientAppPath.Combine(_refreshJsFile);
        string targetRefreshJs = workspace.ClientBuildPath.Combine(_refreshJsFile);

        if (File.Exists(sourceRefreshJsApp))
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        else
            Console.WriteLine($"Warning: {_refreshJsFile} not found in {sourceRefreshJsApp}");

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        foreach (string jsFile in Directory.GetFiles(workspace.ClientBuildPath, "*.js", SearchOption.AllDirectories))
        {
            // Skip refresh.js as it's only for development
            if (Path.GetFileName(jsFile).Equals(_refreshJsFile, StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath = Path.GetRelativePath(workspace.ClientBuildPath, jsFile);
            string targetFilePath = Path.Combine(workspace.ClientDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFile);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        await Task.CompletedTask;
    }

    private void CompileTypeScriptFiles()
    {
        var baseTsConfigPath = workspace.WorkingPath.Combine("base.tsconfig.json");

        var processInfo = new ProcessStartInfo
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
            var errorMessage = $"TypeScript compilation failed (Exit Code: {process.ExitCode})";
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
        var packageLockPath = workspace.WorkingPath.Combine(Files.PackageLockJson);
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
        var pageDirectory = workspace.ClientPagesPath.CreateSubDirectory(pageName);
        var tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        var tsContent = $"""
            import '../../app/workspace.js';

            console.log('{pageName} page loaded');
            """;
        File.WriteAllText(tsFilePath, tsContent);
        await Task.CompletedTask;
    }
}