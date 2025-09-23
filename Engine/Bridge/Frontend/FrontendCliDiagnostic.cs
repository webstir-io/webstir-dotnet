using System.Collections.Generic;
using System.Text.Json;

namespace Engine.Bridge.Frontend;

internal sealed class FrontendCliDiagnostic
{
    public string Type { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public Dictionary<string, JsonElement>? Data
    {
        get; init;
    }

    public string? Suggestion
    {
        get; init;
    }
}
