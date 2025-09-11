using System;
using System.Collections.Generic;
using System.Text;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsModuleTransformer
{
    public static JsTransformedModule Transform(JsModuleInfo module, int moduleId, Dictionary<string, int> moduleIdMap)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moduleIdMap);

        StringBuilder output = new();

        output.AppendLine(FormattableString.Invariant($"{Js.CommentPrefix} Module {moduleId}: {module.FilePath}"));
        output.AppendLine(FormattableString.Invariant($"{Syntax.OpenParen}function{Syntax.OpenParen}{Syntax.CloseParen} {Syntax.OpenBrace}"));

        AppendImports(output, module.Imports, moduleIdMap);
        AppendModuleContent(output, module.Content);
        AppendExports(output, module.Exports, moduleId);

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

    private static void AppendImports(StringBuilder output, List<JsImportStatement> imports, Dictionary<string, int> moduleIdMap)
    {
        foreach (JsImportStatement import in imports)
        {
            if (import.ResolvedPath == null || !moduleIdMap.TryGetValue(import.ResolvedPath, out int sourceModuleId))
            {
                continue;
            }

            if (import.DefaultSpecifier != null)
            {
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {import.DefaultSpecifier}{Syntax.Assignment}{Js.GetModuleDefault(sourceModuleId)}{Syntax.Semicolon}"));
            }

            foreach (string specifier in import.Specifiers)
            {
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {specifier}{Syntax.Assignment}{Js.GetModuleExport(sourceModuleId, specifier)}{Syntax.Semicolon}"));
            }

            if (import.NamespaceSpecifier != null)
            {
                output.AppendLine(FormattableString.Invariant($"  {Js.Const} {import.NamespaceSpecifier}{Syntax.Assignment}{Js.GetModuleVar(sourceModuleId)}{Syntax.Semicolon}"));
            }
        }
    }

    private static void AppendModuleContent(StringBuilder output, string content)
    {
        string cleanCode = RemoveImportsAndExports(content);
        output.AppendLine(cleanCode);
    }

    private static void AppendExports(StringBuilder output, List<JsExportStatement> exports, int moduleId)
    {
        foreach (JsExportStatement export in exports)
        {
            if (export.IsDefault)
            {
                output.AppendLine(FormattableString.Invariant($"  {Js.Var} {Js.GetModuleDefault(moduleId)}{Syntax.Assignment}undefined{Syntax.Semicolon}"));
            }

            foreach (string specifier in export.Specifiers)
            {
                output.AppendLine(FormattableString.Invariant($"  {Js.Var} {Js.GetModuleExport(moduleId, specifier)}{Syntax.Assignment}{specifier}{Syntax.Semicolon}"));
            }
        }
    }

    private static string RemoveImportsAndExports(string code)
    {
        code = JsRegex.ImportStatement().Replace(code, string.Empty);
        code = JsRegex.ExportKeyword().Replace(code, string.Empty);

        return code;
    }
}

