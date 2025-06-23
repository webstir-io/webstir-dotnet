using System.Diagnostics;
using CLI.Helpers;
using CLI.Interfaces;

namespace CLI.Workers;

public class ScriptsWorker() : IFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _indexTsFile = "index.ts";
    private const string _refreshJsFile = "refresh.js";

    public int BuildOrder { get; } = 1;

    public void Init()
    {
        if (!File.Exists(_tsConfigFile))
            AssemblyHelpers.WriteResourceToFile(_tsConfigFile, _tsConfigFile);

        string outputRefreshJsFilepath = Directories.AppDirectory.Join(_refreshJsFile);
        if (!File.Exists(outputRefreshJsFilepath))
            AssemblyHelpers.WriteResourceToFile(_refreshJsFile, outputRefreshJsFilepath);

        string outputAppTsFilepath = Directories.AppDirectory.Join(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_appTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = Directories.IndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputIndexTsFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        CompileTypeScriptFiles();

        if (!releaseMode)
        {
            string sourceRefreshJsApp = Directories.AppDirectory.Join(_refreshJsFile);
            string targetRefreshJs = Directories.BuildDirectory.Join(_refreshJsFile);

            if (File.Exists(sourceRefreshJsApp))
            {
                File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
            }
            else
            {
                Console.WriteLine($"Warning: {_refreshJsFile} not found in {sourceRefreshJsApp}");
            }
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(Directories.DistDirectory.FullName); // Ensure dist directory exists

        // Copy all .js files from BuildDirectory to DistDirectory, maintaining subfolder structure
        foreach (FileInfo jsFile in Directories.BuildDirectory.GetFiles("*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(Directories.BuildDirectory.FullName, jsFile.FullName);
            string targetFilePath = Path.Combine(Directories.DistDirectory.FullName, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!); // Ensure sub-directory exists in target
            
            // Read JS content and remove comments
            string jsContent = File.ReadAllText(jsFile.FullName);
            jsContent = RemoveJavaScriptComments(jsContent);
            
            File.WriteAllText(targetFilePath, jsContent);
        }
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var tsFilePath = pageDirectory.Join($"{pageName}.ts");
        File.WriteAllText(tsFilePath, $"// TypeScript file for {pageName} page\n");
    }

    private static void CompileTypeScriptFiles()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "tsc",
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