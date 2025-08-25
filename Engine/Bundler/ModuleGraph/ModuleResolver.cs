using System.Text.Json;
using Engine.Extensions;

namespace Engine.Bundler.ModuleGraph;

public class ModuleResolver(AppWorkspace workspace)
{
    private static readonly string[] Extensions = [ModuleConstants.Extensions.TypeScript, ModuleConstants.Extensions.JavaScript, ModuleConstants.Extensions.ModuleJs, ModuleConstants.Extensions.Json];
    private static readonly string[] IndexFiles = [$"{Files.Index}{ModuleConstants.Extensions.TypeScript}", $"{Files.Index}{ModuleConstants.Extensions.JavaScript}", $"{Files.Index}{ModuleConstants.Extensions.ModuleJs}"];

    public string? ResolvePath(string importPath, string fromFile)
    {
        if (IsRelativePath(importPath))
            return ResolveRelativePath(importPath, fromFile);
        
        if (IsAbsolutePath(importPath))
            return ResolveAbsolutePath(importPath);
        
        return ResolveBareImport(importPath);
    }

    private static bool IsRelativePath(string path)
    {
        return path.StartsWith(ModuleConstants.Prefixes.Relative) || path.StartsWith(ModuleConstants.Prefixes.ParentRelative);
    }

    private static bool IsAbsolutePath(string path)
    {
        return path.StartsWith('/');
    }

    private static string? ResolveRelativePath(string importPath, string fromFile)
    {
        string directory = Path.GetDirectoryName(fromFile) ?? string.Empty;
        string resolvedPath = Path.Combine(directory, importPath);
        resolvedPath = Path.GetFullPath(resolvedPath);

        if (File.Exists(resolvedPath))
            return resolvedPath;

        foreach (string extension in Extensions)
        {
            string pathWithExtension = resolvedPath + extension;
            if (File.Exists(pathWithExtension))
                return pathWithExtension;
        }

        if (Directory.Exists(resolvedPath))
        {
            foreach (string indexFile in IndexFiles)
            {
                string indexPath = Path.Combine(resolvedPath, indexFile);
                if (File.Exists(indexPath))
                    return indexPath;
            }
        }

        return null;
    }

    private string? ResolveAbsolutePath(string importPath)
    {
        string basePath = workspace.SrcPath;
        string resolvedPath = Path.Combine(basePath, importPath.TrimStart('/'));
        resolvedPath = Path.GetFullPath(resolvedPath);

        if (File.Exists(resolvedPath))
            return resolvedPath;

        foreach (string extension in Extensions)
        {
            string pathWithExtension = resolvedPath + extension;
            if (File.Exists(pathWithExtension))
                return pathWithExtension;
        }

        if (Directory.Exists(resolvedPath))
        {
            foreach (string indexFile in IndexFiles)
            {
                string indexPath = Path.Combine(resolvedPath, indexFile);
                if (File.Exists(indexPath))
                    return indexPath;
            }
        }

        return null;
    }

    private string? ResolveBareImport(string importPath)
    {
        string nodeModulesPath = workspace.WorkingPath.Combine(Folders.NodeModules);
        
        if (!Directory.Exists(nodeModulesPath))
            return null;

        string packagePath = Path.Combine(nodeModulesPath, importPath);

        if (File.Exists(packagePath))
            return packagePath;

        foreach (string extension in Extensions)
        {
            string pathWithExtension = packagePath + extension;
            if (File.Exists(pathWithExtension))
                return pathWithExtension;
        }

        if (Directory.Exists(packagePath))
        {
            string packageJsonPath = Path.Combine(packagePath, Files.PackageJson);
            if (File.Exists(packageJsonPath))
            {
                string? mainFile = GetMainFileFromPackageJson(packageJsonPath);
                if (mainFile != null)
                {
                    string mainPath = Path.Combine(packagePath, mainFile);
                    if (File.Exists(mainPath))
                        return mainPath;
                }
            }

            foreach (string indexFile in IndexFiles)
            {
                string indexPath = Path.Combine(packagePath, indexFile);
                if (File.Exists(indexPath))
                    return indexPath;
            }
        }

        return null;
    }

    private static string? GetMainFileFromPackageJson(string packageJsonPath)
    {
        string content = File.ReadAllText(packageJsonPath);
        JsonDocument doc = JsonDocument.Parse(content);
        
        if (doc.RootElement.TryGetProperty(ModuleConstants.PackageJsonFields.Main, out JsonElement mainElement))
            return mainElement.GetString();
        
        if (doc.RootElement.TryGetProperty(ModuleConstants.PackageJsonFields.Module, out JsonElement moduleElement))
            return moduleElement.GetString();
        
        return null;
    }

    public static bool IsNodeModule(string filePath)
    {
        return filePath.Contains(Folders.NodeModules);
    }
}