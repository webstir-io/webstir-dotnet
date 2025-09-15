using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Engine.Pipelines.Core.Utilities;

public enum DiagnosticLevel
{
    Information,
    Warning,
    Error
}

public class Diagnostic
{
    public required DiagnosticLevel Level
    {
        get; init;
    }
    public required string Message
    {
        get; init;
    }
    public string? File
    {
        get; init;
    }
    public int? Line
    {
        get; init;
    }
    public int? Column
    {
        get; init;
    }

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(File) && Line.HasValue)
            return $"{File}({Line},{Column ?? 0}): {Level}: {Message}";
        return $"{Level}: {Message}";
    }

    public static Diagnostic Error(string message, string? file = null, int? line = null, int? column = null) => new()
    {
        Level = DiagnosticLevel.Error,
        Message = message,
        File = file,
        Line = line,
        Column = column
    };
}

public class DiagnosticCollection
{
    private readonly List<Diagnostic> _items = [];

    public IReadOnlyList<Diagnostic> All => _items;
    public IEnumerable<Diagnostic> Errors => _items.Where(d => d.Level == DiagnosticLevel.Error);
    public IEnumerable<Diagnostic> Warnings => _items.Where(d => d.Level == DiagnosticLevel.Warning);

    public bool HasErrors => Errors.Any();

    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);
    public void AddError(string message, string? file = null, int? line = null, int? column = null) =>
        _items.Add(Diagnostic.Error(message, file, line, column));

    public void ParseAndAddErrors(string text, Regex pattern, string fallbackMessage) => ParseAndAddErrors(text, [pattern], fallbackMessage);

    public void ParseAndAddErrors(string text, IEnumerable<Regex> patterns, string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        if (string.IsNullOrWhiteSpace(text))
        {
            AddError(fallbackMessage);
            return;
        }

        int added = 0;
        foreach (Regex pattern in patterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                string file = match.Groups["file"].Value.Trim();
                int line = int.TryParse(match.Groups["line"].Value, out int ln) ? ln : 0;
                int col = int.TryParse(match.Groups["col"].Value, out int cl) ? cl : 0;
                string message = match.Groups["msg"].Value.Trim();
                AddError(message, file, line, col);
                added++;
            }
        }

        if (added == 0)
        {
            AddError(fallbackMessage);
        }
    }
}
