using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers.Shared;

public class SharedWorker(AppContext context) : IWorker
{
    private const string _sharedFolder = "shared";
    private const string _typesFolder = "types";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder => 3; // Fast operation, can run with other fast operations

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    {
        var sharedTypesDirectory = context.SharedPath.CreateSubDirectory(_typesFolder);
        var indexTsPath = sharedTypesDirectory.Combine(_indexTsFile);
        if (!File.Exists(indexTsPath))
        {
            var resourcePath = $"{_sharedFolder}.{_typesFolder}";
            AssemblyHelpers.WriteResourceToFile(resourcePath, _indexTsFile, indexTsPath);
        }

        var routerTypesPath = context.SharedPath.Combine("router-types.ts");
        if (!File.Exists(routerTypesPath))
            AssemblyHelpers.WriteResourceToFile(_sharedFolder, "router-types.ts", routerTypesPath);
            
        await Task.CompletedTask;
    }

    public async Task BuildAsync(bool releaseMode = false)
    { 
        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        await Task.CompletedTask;
    }
}