using System;
using System.Collections.Generic;
using System.Text;
using Engine.Pipelines.JavaScript.Common;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Transformation;

public static class JsModuleTransformer
{
    public static JsTransformedModule Transform(JsModuleInfo module, int moduleId, Dictionary<string, int> moduleIdMap, bool emitComments = true, bool compactNames = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moduleIdMap);

        StringBuilder output = new();

        if (emitComments)
        {
            output.AppendLine(FormattableString.Invariant($"{Js.CommentPrefix} Module {moduleId}: {module.FilePath}"));
        }
        output.AppendLine(FormattableString.Invariant($"{Syntax.OpenParen}function{Syntax.OpenParen}{Syntax.CloseParen} {Syntax.OpenBrace}"));

        AppendImports(output, module.Imports, moduleIdMap, compactNames);
        AppendModuleContent(output, module.Content);
        AppendExports(output, module.Exports, moduleId, compactNames);

        output.AppendLine(FormattableString.Invariant($"{Syntax.CloseBrace}{Syntax.CloseParen}{Syntax.OpenParen}{Syntax.CloseParen}{Syntax.Semicolon}"));

        string code = output.ToString();

        if (JsScopeHoister.CanHoist(module))
        {
            code = JsScopeHoister.HoistScope(code, moduleId);
        }

        if (JsTreeShaker.HasUnusedExports(module, []))
        {
            code = JsTreeShaker.RemoveUnusedCode(code, module, []);
        }

        return new JsTransformedModule
        {
            Id = moduleId,
            Code = code,
            SourceMap = null
        };
    }

    private static void AppendImports(StringBuilder output, List<JsImportStatement> imports, Dictionary<string, int> moduleIdMap, bool compactNames)
    {
        foreach (JsImportStatement import in imports)
        {
            if (import.ResolvedPath == null || !moduleIdMap.TryGetValue(import.ResolvedPath, out int sourceModuleId))
            {
                continue;
            }

            if (import.DefaultSpecifier != null)
            {
                string defVar = GetDefaultVar(sourceModuleId, compactNames);
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {import.DefaultSpecifier}{Syntax.Assignment}{defVar}{Syntax.Semicolon}"));
            }

            foreach (string specifier in import.Specifiers)
            {
                string expVar = GetExportVar(sourceModuleId, specifier, compactNames);
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {specifier}{Syntax.Assignment}{expVar}{Syntax.Semicolon}"));
            }

            if (import.NamespaceSpecifier != null)
            {
                string nsVar = GetModuleVar(sourceModuleId, compactNames);
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {import.NamespaceSpecifier}{Syntax.Assignment}{nsVar}{Syntax.Semicolon}"));
            }
        }
    }

    private static void AppendModuleContent(StringBuilder output, string content)
    {
        string cleanCode = RemoveImportsAndExports(content);
        output.AppendLine(cleanCode);
    }

    private static void AppendExports(StringBuilder output, List<JsExportStatement> exports, int moduleId, bool compactNames)
    {
        foreach (JsExportStatement export in exports)
        {
            if (export.IsDefault)
            {
                string defVar = GetDefaultVar(moduleId, compactNames);
                output.AppendLine(FormattableString.Invariant($"  {Js.Var} {defVar}{Syntax.Assignment}undefined{Syntax.Semicolon}"));
            }

            foreach (string specifier in export.Specifiers)
            {
                string expVar = GetExportVar(moduleId, specifier, compactNames);
                output.AppendLine(FormattableString.Invariant($"  {Js.Var} {expVar}{Syntax.Assignment}{specifier}{Syntax.Semicolon}"));
            }
        }
    }

    private static string RemoveImportsAndExports(string code)
    {
        code = JsRegex.ImportStatement().Replace(code, string.Empty);
        code = JsRegex.ExportKeyword().Replace(code, string.Empty);

        return code;
    }

    private static string GetModuleVar(int moduleId, bool compactNames) => compactNames ? $"m{moduleId}" : Js.GetModuleVar(moduleId);

    private static string GetDefaultVar(int moduleId, bool compactNames) => compactNames ? $"md{moduleId}" : Js.GetModuleDefault(moduleId);

    private static string GetExportVar(int moduleId, string exportName, bool compactNames)
    {
        if (!compactNames)
        {
            return Js.GetModuleExport(moduleId, exportName);
        }

        string shortToken = MapExportName(exportName);
        return $"m{moduleId}{shortToken}";
    }

    private static string MapExportName(string name)
    {
        // Frequent names → shortest tokens. Otherwise, use '_' + original name.
        // Deterministic and per‑module safe.
        return name switch
        {
            "render" => "a",
            "init" => "b",
            "setup" => "c",
            "start" => "d",
            "stop" => "e",
            "create" => "f",
            "destroy" => "g",
            "mount" => "h",
            "unmount" => "i",
            "onEnter" => "j",
            "onLeave" => "k",
            "onUpdate" => "l",
            "load" => "m",
            "save" => "n",
            "open" => "o",
            "close" => "p",
            "fetch" => "q",
            "post" => "r",
            "get" => "s",
            "set" => "t",
            "update" => "u",
            "remove" => "v",
            "dispose" => "w",
            "navigate" => "x",
            "handle" => "y",
            "build" => "z",

            _ => "_" + name
        };
    }
}
