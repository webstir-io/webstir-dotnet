using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workers;

public class SharedWorker : IFileWorker
{
    private const string _sharedFolder = "shared";
    private const string _typesFolder = "types";
    private const string _indexTsFile = "index.ts";
    
    public int BuildOrder { get; } = 0; // Doesn't participate in build
    
    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        // Only create shared folder for Fullstack mode
        if (mode != ProjectMode.Fullstack)
            return;
            
        // Create shared/types directory
        var sharedTypesDirectory = Directories.SharedDirectory.SubDirectory(_typesFolder);
        
        // Create index.ts file from Resources
        var indexTsPath = sharedTypesDirectory.Join(_indexTsFile);
        if (!File.Exists(indexTsPath))
        {
            var resourcePath = $"{_sharedFolder}.{_typesFolder}";
            AssemblyHelpers.WriteResourceToFile(resourcePath, _indexTsFile, indexTsPath);
        }
        
        // Create router-types.ts file
        var routerTypesPath = Directories.SharedDirectory.Join("router-types.ts");
        if (!File.Exists(routerTypesPath))
        {
            AssemblyHelpers.WriteResourceToFile(_sharedFolder, "router-types.ts", routerTypesPath);
        }
    }
    
    public void Build(bool releaseMode = false)
    {
        // Shared types are compiled by ScriptsWorker and ServerWorker
        return;
    }
    
    public void Publish()
    {
        // Shared types are handled by other workers during publish
        return;
    }
}