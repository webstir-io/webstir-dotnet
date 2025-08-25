namespace Engine.Bundler.ModuleGraph;

public class ModuleGraphBuilder(
    ModuleResolver resolver,
    ModuleGraph graph)
{
    private readonly HashSet<string> _processedFiles = [];

    public async Task<ModuleGraph> BuildGraphAsync(params string[] entryPoints)
    {
        if (entryPoints.Length == 0)
            return graph;
        
        _processedFiles.Clear();
        
        List<Task> tasks = [];
        foreach (string entryPoint in entryPoints)
        {
            tasks.Add(ProcessModuleAsync(entryPoint));
            graph.MarkAsEntryPoint(entryPoint);
        }
        
        await Task.WhenAll(tasks);
        
        return graph;
    }

    private async Task ProcessModuleAsync(string filePath)
    {
        if (!_processedFiles.Add(filePath))
            return;
        
        string content = await File.ReadAllTextAsync(filePath);
        ModuleInfo moduleInfo = ModuleParser.ParseModule(filePath, content);
        List<string> resolvedDependencies = [];
        List<Task> dependencyTasks = [];
        
        foreach (ImportStatement import in moduleInfo.Imports)
        {
            string? resolvedPath = resolver.ResolvePath(import.Source, filePath)
                ?? throw new InvalidOperationException($"Cannot resolve import '{import.Source}' from {filePath}");

            resolvedDependencies.Add(resolvedPath);
            
            if (!ModuleResolver.IsNodeModule(resolvedPath) && !_processedFiles.Contains(resolvedPath))
                dependencyTasks.Add(ProcessModuleAsync(resolvedPath));
        }
        
        graph.AddModule(filePath, moduleInfo, resolvedDependencies);
        await Task.WhenAll(dependencyTasks);
    }
}