using System.Diagnostics;
using CLI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CLI.Workers;

public class ScriptsWorker : IWebFileWorker
{
    private const string _tsConfigFile = "tsconfig.json";
    private const string _appTsFile = "app.ts";
    private const string _appJsFile = "app.js";
    private const string _indexTsFile = "index.ts";
    private const string _refreshTsFile = "refresh.ts";
    private const string _refreshJsFile = "refresh.js";

    public int BuildOrder { get; } = 1;

    public void Init()
    {
        if (!File.Exists(_tsConfigFile))
            AssemblyHelpers.WriteResourceToFile(_tsConfigFile, _tsConfigFile);

        var outputRefreshTsFilepath = Directories.AppDirectory.Join(_refreshTsFile);
        if (!File.Exists(outputRefreshTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_refreshTsFile, outputRefreshTsFilepath);

        var outputAppTsFilepath = Directories.AppDirectory.Join(_appTsFile);
        if (!File.Exists(outputAppTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputAppTsFilepath);

        var outputIndexTsFilepath = Directories.IndexDirectory.Join(_indexTsFile);
        if (!File.Exists(outputIndexTsFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexTsFile, outputIndexTsFilepath);
    }

    public void Build(bool releaseMode = false)
    { 
        var process = Process.Start("tsc");
        process.WaitForExit();

        if (process.ExitCode != 0)
            return;

        var appJsBuildFilepath = Directories.BuildDirectory
            .Join(Settings.AppFolder)
            .Join(_appJsFile);

        var jsLines = File.ReadAllLines(appJsBuildFilepath).ToList();

        if (!releaseMode)
        {
            var refreshJsBuildFilepath = Directories.BuildDirectory
                .Join(Settings.AppFolder)
                .Join(_refreshJsFile);

            jsLines.AddRange(File.ReadAllLines(refreshJsBuildFilepath).Skip(1));
        }
        
        foreach (var pageDirectory in Directories.BuildPagesDirectory.GetDirectories())
        {
            foreach (var jsFile in pageDirectory.GetFiles("*.js", SearchOption.AllDirectories))
            {
                // Skip the first line that the TypeScript compiler adds for each file
                jsLines.AddRange(File.ReadAllLines(jsFile.FullName).Skip(1));                
            }

            var pageJsFile = Directories.BinDirectory.Join($"{pageDirectory.Name}.js");
            File.WriteAllLines(pageJsFile, jsLines);
        }
    }

    public void Publish()
    {
        foreach (var file in Directories.BinDirectory.GetFiles("*.js"))
            file.CopyTo($"{Directories.DistDirectory.FullName}/{file.Name}");
    }
}