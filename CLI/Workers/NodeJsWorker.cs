using System.Diagnostics;
using CLI.Helpers;
using CLI.Interfaces;

namespace CLI.Workers;

public class NodeJsWorker() : IFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder { get; } = 1; // Run parallel with other workers

    public void Init()
    {
        var serverDirectory = GetServerDirectory();
        if (!serverDirectory.Exists)
            return;

        string tsConfigPath = serverDirectory.Join(_tsConfigFile);
        if (!File.Exists(tsConfigPath))
            AssemblyHelpers.WriteResourceToFile("server.tsconfig.json", tsConfigPath);

        string indexTsPath = serverDirectory.Join(_indexTsFile);
        if (!File.Exists(indexTsPath))
            AssemblyHelpers.WriteResourceToFile("server.index.ts", indexTsPath);
    }

    public void Build(bool releaseMode = false)
    {
        var serverDirectory = GetServerDirectory();
        if (!serverDirectory.Exists)
            return;

        CompileServerTypeScript(serverDirectory);
    }

    public void Publish()
    {
        var serverDirectory = GetServerDirectory();
        if (!serverDirectory.Exists)
            return;

        var serverBuildDirectory = GetServerBuildDirectory();
        var serverDistDirectory = GetServerDistDirectory();
        
        Directory.CreateDirectory(serverDistDirectory.FullName);

        // Copy all .js files from server build to server dist
        foreach (FileInfo jsFile in serverBuildDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(serverBuildDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(serverDistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            
            // Read JS content and remove comments
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        // Server worker doesn't handle page creation
    }

    private static DirectoryInfo GetServerDirectory()
    {
        return new DirectoryInfo(Path.Combine(Settings.SourceFolder, "server"));
    }

    private static DirectoryInfo GetServerBuildDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(Settings.BuildFolder, "server"));
    }

    private static DirectoryInfo GetServerDistDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(Settings.DistFolder, "server"));
    }

    private static void CompileServerTypeScript(DirectoryInfo serverDirectory)
    {
        var tsConfigPath = serverDirectory.Join(_tsConfigFile);
        
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