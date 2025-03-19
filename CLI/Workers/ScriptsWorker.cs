using System.Diagnostics;
using System.Text.RegularExpressions;
using CLI.Bundlers;
using CLI.Helpers;

namespace CLI.Workers;

public class ScriptsWorker(ScriptBundler scriptBundler) : IWebFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _appJsFile = "app.js";
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
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputAppTsFilepath);

        string outputIndexTsFilepath = Directories.IndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputIndexTsFilepath);
    }

    public void Build(bool releaseMode = false)
    { 
        ComplileTypeScriptFiles();

        if (!releaseMode)
        {
            string refreshJsFilepath = Directories.AppDirectory.Join(_refreshJsFile);
            File.Copy(refreshJsFilepath, Directories.BinDirectory.Join(_refreshJsFile), true);
        }

        string appJsBuildFilepath = Directories.BuildDirectory
            .Join(Settings.AppFolder)
            .Join(_appJsFile);

        foreach (DirectoryInfo pageDirectory in Directories.BuildPagesDirectory.GetDirectories())
        {
            List<string> jsLines = [.. File.ReadAllLines(appJsBuildFilepath)];
            foreach (FileInfo jsFile in pageDirectory.GetFiles("*.js", SearchOption.AllDirectories))
            {
                scriptBundler.Bundle(jsFile.FullName);
                // jsLines.AddRange(BuildDependencies(jsFile.FullName));
            }

            string pageJsFile = Directories.BinDirectory.Join($"{pageDirectory.Name}.js");
            File.WriteAllLines(pageJsFile, jsLines);
        }
    }

    public void Publish()
    {
        foreach (var file in Directories.BinDirectory.GetFiles("*.js"))
            file.CopyTo($"{Directories.DistDirectory.FullName}/{file.Name}");
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        File.Create(pageDirectory.Join($"{pageName}.ts")).Close();
    }

    private static void ComplileTypeScriptFiles()
    {
        var process = Process.Start("tsc");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception("TypeScript compilation failed.");
    }

    // private static string BuildDependencies(string entryFile)
    // {
    //     var modules = new Dictionary<string, string>();
    //     BuildDependencyGraph(entryFile, modules);

    //     // Identify used functions/variables
    //     var usedSymbols = new HashSet<string>();
    //     CollectUsedSymbols(entryFile, modules, usedSymbols);

    //     // Merge all modules into a single file with tree shaking
    //     string bundle = string.Empty;
    //     foreach (var module in modules)
    //     {
    //         bundle += $"// Module: {module.Key}\n";
    //         bundle += RemoveUnusedExports(module.Value, usedSymbols) + "\n\n";
    //     }

    //     return bundle;
    // }

    // private static void BuildDependencyGraph(string filePath, Dictionary<string, string> modules)
    // {
    //     if (modules.ContainsKey(filePath))
    //         return;

    //     // This is slightly inefficient, but it simplifies the code
    //     List<string> fileLines = [.. File.ReadAllLines(filePath)];
    //     List<string> dependencies = ParseDependencies(string.Join("\n", fileLines));
    //     fileLines.RemoveAll(line => line.StartsWith("import"));
    //     var bundledJs = string.Join(Environment.NewLine, fileLines);
    //         // .Replace("export default", string.Empty)
    //         // .Replace("export", string.Empty);

    //     modules[filePath] = bundledJs;

    //     // Resolve dependencies recursively
    //     foreach (var dependency in dependencies)
    //     {
    //         string dependencyPath;
    //         if (dependency.StartsWith('$'))
    //         {
    //             dependencyPath = Directories.BuildDirectory.Join($"{dependency[1..]}.js");
    //         }
    //         else 
    //         {
    //             throw new NotImplementedException("Node modules are not supported yet.");
    //         }
            
    //         if (!File.Exists(dependencyPath))
    //             throw new Exception($"Module file '{dependencyPath}' not found");

    //         BuildDependencyGraph(dependencyPath, modules);
    //     }
    // }

    // private static List<string> ParseDependencies(string jsCode)
    // {
    //     var dependencies = new List<string>();

    //     // Exclude single-line comments (//...)
    //     jsCode = Regex.Replace(jsCode, @"//.*?$", "", RegexOptions.Multiline);

    //     // Exclude Remove multi-line comments (/* ... */)
    //     jsCode = Regex.Replace(jsCode, @"/\*.*?\*/", "", RegexOptions.Singleline);

    //     // Find import statements that remain
    //     var importRegex = new Regex(@"import\s+.*?['""](.+?)['""];", RegexOptions.Compiled);

    //     foreach (Match match in importRegex.Matches(jsCode))
    //         dependencies.Add(match.Groups[1].Value);

    //     return dependencies;
    // }

    // private static void CollectUsedSymbols(string filePath, Dictionary<string, string> modules, HashSet<string> usedSymbols)
    // {
    //     if (!modules.TryGetValue(filePath, out string? content)) 
    //         return;
        
    //     var functionCallRegex = new Regex(@"\b(\w+)\s*\(", RegexOptions.Compiled);
    //     foreach (Match match in functionCallRegex.Matches(content))
    //     {
    //         usedSymbols.Add(match.Groups[1].Value);
    //     }

    //     foreach (var dependency in ParseDependencies(content))
    //     {
    //         string directoryName = Path.GetDirectoryName(filePath) 
    //             ?? throw new ArgumentException($"Invalid file path: {filePath}");
            
    //         string dependencyPath = Path.Combine(directoryName, dependency);
    //         CollectUsedSymbols(dependencyPath, modules, usedSymbols);
    //     }
    // }

    // static string RemoveUnusedExports(string jsCode, HashSet<string> usedSymbols)
    // {
    //     var exportRegex = new Regex(@"export\s+(function|const|let|var|class)\s+(\w+)", RegexOptions.Compiled);
    //     string newCode = jsCode;

    //     foreach (Match match in exportRegex.Matches(jsCode))
    //     {
    //         string exportedName = match.Groups[2].Value;
    //         if (!usedSymbols.Contains(exportedName))
    //         {
    //             // Remove the export keyword but keep the function definition
    //             newCode = newCode.Replace(match.Value, match.Value.Replace("export ", ""));
    //         }
    //     }

    //     return newCode;
    // }
}