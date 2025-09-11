using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Engine.Pipelines.JavaScript.Parsing;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsModuleParser
{
    private static void MapImports(List<ImportDeclaration> imports, JsModuleInfo module)
    {
        foreach (ImportDeclaration imp in imports)
        {
            JsImportStatement item = new()
            {
                Source = imp.Source,
                LineNumber = imp.Line,
                IsDynamic = imp.IsDynamic
            };

            if (imp.IsSideEffect)
            {
                item.Type = JsImportType.SideEffect;
            }
            else if (imp.IsDynamic)
            {
                item.Type = JsImportType.Dynamic;
            }
            else if (imp.DefaultImport != null && imp.NamedImports != null && imp.NamedImports.Count > 0)
            {
                item.Type = JsImportType.Mixed;
                item.DefaultSpecifier = imp.DefaultImport;
                item.Specifiers = [.. imp.NamedImports.Select(n => n.Local)];
            }
            else if (imp.DefaultImport != null)
            {
                item.Type = JsImportType.Default;
                item.DefaultSpecifier = imp.DefaultImport;
            }
            else if (imp.NamespaceImport != null)
            {
                item.Type = JsImportType.Namespace;
                item.NamespaceSpecifier = imp.NamespaceImport;
            }
            else if (imp.NamedImports != null && imp.NamedImports.Count > 0)
            {
                item.Type = JsImportType.Named;
                item.Specifiers = [.. imp.NamedImports.Select(n => n.Local)];
            }

            module.Imports.Add(item);
        }
    }

    private static void MapExports(List<ExportDeclaration> exports, JsModuleInfo module)
    {
        foreach (ExportDeclaration ex in exports)
        {
            JsExportStatement item = new()
            {
                IsDefault = ex.IsDefault,
                LineNumber = ex.Line,
                Source = ex.Source
            };

            if (ex.IsDefault)
            {
                item.Type = JsExportType.Default;
            }
            else if (ex.IsReExport && ex.Namespace != null)
            {
                item.Type = JsExportType.NamespaceReexport;
                item.Specifiers = [ex.Namespace];
            }
            else if (ex.All && ex.IsReExport)
            {
                item.Type = JsExportType.AllReexport;
            }
            else if (ex.Named != null && ex.Named.Count > 0 && ex.IsReExport)
            {
                item.Type = JsExportType.Named;
                item.Specifiers = ex.Named;
            }
            else if (ex.Named != null && ex.Named.Count > 0)
            {
                item.Type = JsExportType.Named;
                item.Specifiers = ex.Named;
            }

            module.Exports.Add(item);
        }
    }

    public static JsModuleInfo ParseModule(string filePath, string content)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(content);
        JsModuleInfo module = new()
        {
            FilePath = filePath,
            Content = content,
            Type = DetectModuleType(filePath, content)
        };

        string cleanContent = RemoveComments(content);

        // Prefer tokenizer-based parsing for robustness
        try
        {
            JavaScriptParser parser = new(content, filePath);
            List<ImportDeclaration> imports = parser.ParseImports();
            List<ExportDeclaration> exports = parser.ParseExports();

            MapImports(imports, module);
            MapExports(exports, module);
        }
        catch
        {
            // Fallback to regex if tokenizer path fails
            ParseImports(cleanContent, module);
            ParseExports(cleanContent, module);
        }

        return module;
    }

    private static JsModuleType DetectModuleType(string filePath, string content)
    {
        if (filePath.EndsWith(Exts.TypeScript, StringComparison.Ordinal))
        {
            return JsModuleType.TypeScript;
        }

        if (content.Contains("import ", StringComparison.Ordinal) || content.Contains("export ", StringComparison.Ordinal))
        {
            return JsModuleType.ES6;
        }

        if (content.Contains("require(", StringComparison.Ordinal) || content.Contains("module.exports", StringComparison.Ordinal))
        {
            return JsModuleType.CommonJS;
        }

        return JsModuleType.Unknown;
    }

    private static string RemoveComments(string content)
    {
        string result = JsRegex.MultiLineComment().Replace(content, string.Empty);
        result = JsRegex.SingleLineComment().Replace(result, string.Empty);

        return result;
    }

    private static void ParseImports(string content, JsModuleInfo module)
    {
        string[] lines = content.Split('\n');

        ParseMixedImports(content, module);
        ParseDefaultImports(content, lines, module);
        ParseNamedImports(content, lines, module);
        ParseNamespaceImports(content, module);
        ParseSideEffectImports(content, module);
        ParseDynamicImports(content, module);
    }

    private static void ParseMixedImports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.MixedImport().Matches(content))
        {
            JsImportStatement import = new()
            {
                Source = match.Groups[3].Value,
                Type = JsImportType.Mixed,
                DefaultSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            };

            import.Specifiers = ParseSpecifiers(match.Groups[2].Value);

            module.Imports.Add(import);
        }
    }

    private static void ParseDefaultImports(string content, string[] lines, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.DefaultImport().Matches(content))
        {
            if (JsRegex.MixedImport().IsMatch(lines[GetLineNumber(content, match.Index) - 1]))
            {
                continue;
            }

            module.Imports.Add(new JsImportStatement
            {
                Source = match.Groups[2].Value,
                Type = JsImportType.Default,
                DefaultSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseNamedImports(string content, string[] lines, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.NamedImports().Matches(content))
        {
            if (JsRegex.MixedImport().IsMatch(lines[GetLineNumber(content, match.Index) - 1]))
            {
                continue;
            }

            JsImportStatement import = new()
            {
                Source = match.Groups[2].Value,
                Type = JsImportType.Named,
                LineNumber = GetLineNumber(content, match.Index)
            };

            import.Specifiers = ParseSpecifiers(match.Groups[1].Value);

            module.Imports.Add(import);
        }
    }

    private static void ParseNamespaceImports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.NamespaceImport().Matches(content))
        {
            module.Imports.Add(new JsImportStatement
            {
                Source = match.Groups[2].Value,
                Type = JsImportType.Namespace,
                NamespaceSpecifier = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseSideEffectImports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.SideEffectImport().Matches(content))
        {
            if (IsPartOfOtherImport(content, match.Index))
            {
                continue;
            }

            module.Imports.Add(new JsImportStatement
            {
                Source = match.Groups[1].Value,
                Type = JsImportType.SideEffect,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseDynamicImports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.DynamicImport().Matches(content))
        {
            module.Imports.Add(new JsImportStatement
            {
                Source = match.Groups[1].Value,
                Type = JsImportType.Dynamic,
                IsDynamic = true,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseExports(string content, JsModuleInfo module)
    {
        ParseDefaultExports(content, module);
        ParseReexportNamed(content, module);
        ParseReexportAll(content, module);
        ParseReexportNamespace(content, module);
        ParseNamedExports(content, module);
    }

    private static void ParseDefaultExports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.DefaultExport().Matches(content))
        {
            module.Exports.Add(new JsExportStatement
            {
                Type = JsExportType.Default,
                IsDefault = true,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseReexportNamed(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.ReexportNamed().Matches(content))
        {
            JsExportStatement export = new()
            {
                Type = JsExportType.Named,
                Source = match.Groups[2].Value,
                LineNumber = GetLineNumber(content, match.Index)
            };

            export.Specifiers = ParseSpecifiers(match.Groups[1].Value);

            module.Exports.Add(export);
        }
    }

    private static void ParseReexportAll(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.ReexportAll().Matches(content))
        {
            module.Exports.Add(new JsExportStatement
            {
                Type = JsExportType.AllReexport,
                Source = match.Groups[1].Value,
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseReexportNamespace(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.ReexportNamespace().Matches(content))
        {
            module.Exports.Add(new JsExportStatement
            {
                Type = JsExportType.NamespaceReexport,
                Source = match.Groups[2].Value,
                Specifiers = [match.Groups[1].Value],
                LineNumber = GetLineNumber(content, match.Index)
            });
        }
    }

    private static void ParseNamedExports(string content, JsModuleInfo module)
    {
        foreach (Match match in JsRegex.NamedExports().Matches(content))
        {
            int start = Math.Max(0, match.Index - 50);
            int end = Math.Min(content.Length, match.Index + 50);
            if (JsRegex.ReexportNamed().IsMatch(content[start..end]))
            {
                continue;
            }

            JsExportStatement export = new()
            {
                Type = JsExportType.Named,
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
        int lineEnd = content.IndexOf('\n', index);
        if (lineEnd == -1)
        {
            lineEnd = content.Length;
        }
        string line = content[lineStart..lineEnd];

        return line.Contains(" from ", StringComparison.Ordinal)
            || line.Contains("import {", StringComparison.Ordinal)
            || line.Contains("import *", StringComparison.Ordinal);
    }

    private static int GetLineNumber(string content, int index) => content[..index].Count(c => c == '\n') + 1;
}
