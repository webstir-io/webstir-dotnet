using System.Text.RegularExpressions;

namespace Engine.Bundling.Graph;

public static class ModuleParser
{

    public static ModuleInfo ParseModule(string filePath, string content)
    {
        ModuleInfo module = new()
        {
            FilePath = filePath,
            Content = content,
            Type = DetectModuleType(filePath, content)
        };

        string cleanContent = RemoveComments(content);
        
        ParseImports(cleanContent, module);
        ParseExports(cleanContent, module);

        return module;
    }

    private static ModuleType DetectModuleType(string filePath, string content)
    {
        if (filePath.EndsWith(ModuleConstants.Extensions.TypeScript, StringComparison.Ordinal))
            return ModuleType.TypeScript;
        
        if (content.Contains("import ") || content.Contains("export "))
            return ModuleType.ES6;
        
        if (content.Contains("require(") || content.Contains("module.exports"))
            return ModuleType.CommonJS;
        
        return ModuleType.Unknown;
    }

    private static string RemoveComments(string content)
    {
        string result = ModuleRegex.MultiLineComment().Replace(content, string.Empty);
        result = ModuleRegex.SingleLineComment().Replace(result, string.Empty);

        return result;
    }

    private static void ParseImports(string content, ModuleInfo module)
    {
        string[] lines = content.Split('\n');
        
        ParseMixedImports(content, module);
        ParseDefaultImports(content, lines, module);
        ParseNamedImports(content, lines, module);
        ParseNamespaceImports(content, module);
        ParseSideEffectImports(content, module);
        ParseDynamicImports(content, module);
    }

    private static void ParseMixedImports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.MixedImport().Matches(content))
        {
            ImportStatement import = new()
            {
                Source = match.Groups[3].Value,
                Type = ImportType.Mixed,
                DefaultSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            };
            
            import.Specifiers = ParseSpecifiers(match.Groups[2].Value);
            
            module.Imports.Add(import);
        }
    }

    private static void ParseDefaultImports(string content, string[] lines, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.DefaultImport().Matches(content))
        {
            if (ModuleRegex.MixedImport().IsMatch(lines[GetLineNumber(content, match.Index) - 1]))
                continue;
            
            module.Imports.Add(new ImportStatement
            {
                Source = match.Groups[2].Value,
                Type = ImportType.Default,
                DefaultSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseNamedImports(string content, string[] lines, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.NamedImports().Matches(content))
        {
            if (ModuleRegex.MixedImport().IsMatch(lines[GetLineNumber(content, match.Index) - 1]))
                continue;
            
            ImportStatement import = new()
            {
                Source = match.Groups[2].Value,
                Type = ImportType.Named,
                LineNumber = GetLineNumber(content, match.Index)
            };
            
            import.Specifiers = ParseSpecifiers(match.Groups[1].Value);
            
            module.Imports.Add(import);
        }
    }

    private static void ParseNamespaceImports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.NamespaceImport().Matches(content))
        {
            module.Imports.Add(new ImportStatement
            {
                Source = match.Groups[2].Value,
                Type = ImportType.Namespace,
                NamespaceSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseSideEffectImports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.SideEffectImport().Matches(content))
        {
            if (IsPartOfOtherImport(content, match.Index))
                continue;
            
            module.Imports.Add(new ImportStatement
            {
                Source = match.Groups[1].Value,
                Type = ImportType.SideEffect,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseDynamicImports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.DynamicImport().Matches(content))
        {
            module.Imports.Add(new ImportStatement
            {
                Source = match.Groups[1].Value,
                Type = ImportType.Dynamic,
                IsDynamic = true,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseExports(string content, ModuleInfo module)
    {
        ParseDefaultExports(content, module);
        ParseReexportNamed(content, module);
        ParseReexportAll(content, module);
        ParseReexportNamespace(content, module);
        ParseNamedExports(content, module);
    }

    private static void ParseDefaultExports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.DefaultExport().Matches(content))
        {
            module.Exports.Add(new ExportStatement
            {
                Type = ExportType.Default,
                IsDefault = true,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseReexportNamed(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.ReexportNamed().Matches(content))
        {
            ExportStatement export = new()
            {
                Type = ExportType.Named,
                Source = match.Groups[2].Value,
                LineNumber = GetLineNumber(content, match.Index)
            };
            
            export.Specifiers = ParseSpecifiers(match.Groups[1].Value);
            
            module.Exports.Add(export);
        }
    }

    private static void ParseReexportAll(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.ReexportAll().Matches(content))
        {
            module.Exports.Add(new ExportStatement
            {
                Type = ExportType.AllReexport,
                Source = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseReexportNamespace(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.ReexportNamespace().Matches(content))
        {
            module.Exports.Add(new ExportStatement
            {
                Type = ExportType.NamespaceReexport,
                Source = match.Groups[2].Value,
                Specifiers = [match.Groups[1].Value],
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseNamedExports(string content, ModuleInfo module)
    {
        foreach (Match match in ModuleRegex.NamedExports().Matches(content))
        {
            int start = Math.Max(0, match.Index - 50);
            int end = Math.Min(content.Length, match.Index + 50);
            if (ModuleRegex.ReexportNamed().IsMatch(content[start..end]))
                continue;
            
            ExportStatement export = new()
            {
                Type = ExportType.Named,
                LineNumber = GetLineNumber(content, match.Index)
            };
            
            export.Specifiers = ParseSpecifiers(match.Groups[1].Value);
            
            module.Exports.Add(export);
        }
    }

    private static List<string> ParseSpecifiers(string specifiersString)
    {
        List<string> specifiers = [];
        string[] parts = specifiersString.Split(',');
        
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                string specifier = trimmed.Contains(" as ", StringComparison.Ordinal) 
                    ? trimmed.Split(" as ")[1].Trim()
                    : trimmed;
                specifiers.Add(specifier);
            }
        }

        return specifiers;
    }

    private static bool IsPartOfOtherImport(string content, int index)
    {
        int lineStart = content.LastIndexOf('\n', index) + 1;
        string line = content[lineStart..content.IndexOf('\n', index)];
        
        return line.Contains(" from ", StringComparison.Ordinal) || line.Contains("import {", StringComparison.Ordinal) || line.Contains("import *", StringComparison.Ordinal);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}