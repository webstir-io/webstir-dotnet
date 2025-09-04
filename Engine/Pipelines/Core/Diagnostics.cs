using System.Collections.Generic;
using System.Linq;

namespace Engine.Pipelines.Core;

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
}
