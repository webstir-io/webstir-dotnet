using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workers.Shared;

public class SharedWorker(App app) : ISharedWorker
{
    private const string _sharedFolder = "shared";
    private const string _typesFolder = "types";
    private const string _indexTsFile = "index.ts";

    public int BuildOrder => 2; // Fast operation, run after heavy TypeScript compilation
        
    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {        
        var sharedTypesDirectory = app.SharedDir.CreateSubDirectory(_typesFolder);
        var indexTsPath = sharedTypesDirectory.CombinePath(_indexTsFile);
        if (!File.Exists(indexTsPath))
        {
            var resourcePath = $"{_sharedFolder}.{_typesFolder}";
            AssemblyHelpers.WriteResourceToFile(resourcePath, _indexTsFile, indexTsPath);
        }
        
        var routerTypesPath = app.SharedDir.CombinePath("router-types.ts");
        if (!File.Exists(routerTypesPath))
            AssemblyHelpers.WriteResourceToFile(_sharedFolder, "router-types.ts", routerTypesPath);
    }
    
    public void Build(bool releaseMode = false) { }
    
    public void Publish() { }

    public void AddPage(DirectoryInfo pageDirectory) { }
}