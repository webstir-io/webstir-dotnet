using Engine.Pipelines.Css.Models;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Pipelines.Css.Publish;

public class CssModuleGraph
{
    private readonly Dictionary<string, CssModule> _modules = [];

    public void AddModule(CssModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules[module.FilePath] = module;
    }

    public CssModule? GetModule(string filePath) => _modules.GetValueOrDefault(filePath);
    
    public void Clear() => _modules.Clear();

    public List<CssModule> GetModulesInOrder(params string[] entryPoints)
    {
        ArgumentNullException.ThrowIfNull(entryPoints);
        List<CssModule> ordered = [];
        HashSet<string> processed = [];

        foreach (string entryPoint in entryPoints)
        {
            TraverseModule(entryPoint, ordered, processed);
        }

        return ordered;
    }


    private void TraverseModule(string filePath, List<CssModule> ordered, HashSet<string> processed)
    {
        if (processed.Contains(filePath))
        {
            return;
        }

        processed.Add(filePath);

        if (!_modules.TryGetValue(filePath, out CssModule? module))
        {
            return;
        }

        foreach (CssImport import in module.Imports)
        {
            TraverseModule(import.ResolvedPath, ordered, processed);
        }

        ordered.Add(module);
    }


    public static string GenerateHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..8];
    }
}
