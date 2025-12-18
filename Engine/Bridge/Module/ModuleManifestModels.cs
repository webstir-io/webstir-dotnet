using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Engine.Bridge.Module;

internal sealed class ModuleRuntimeManifest
{
    [JsonPropertyName("contractVersion")]
    public string ContractVersion { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string>? Capabilities
    {
        get; init;
    }

    [JsonPropertyName("assets")]
    public IReadOnlyList<string>? Assets
    {
        get; init;
    }

    [JsonPropertyName("middlewares")]
    public IReadOnlyList<string>? Middlewares
    {
        get; init;
    }

    [JsonPropertyName("routes")]
    public IReadOnlyList<RouteDefinition>? Routes
    {
        get; init;
    }

    [JsonPropertyName("views")]
    public IReadOnlyList<ViewDefinition>? Views
    {
        get; init;
    }

    [JsonPropertyName("jobs")]
    public IReadOnlyList<JobDefinition>? Jobs
    {
        get; init;
    }

    [JsonPropertyName("events")]
    public IReadOnlyList<EventDefinition>? Events
    {
        get; init;
    }

    [JsonPropertyName("services")]
    public IReadOnlyList<ServiceDefinition>? Services
    {
        get; init;
    }

    [JsonPropertyName("init")]
    public string? Init
    {
        get; init;
    }

    [JsonPropertyName("dispose")]
    public string? Dispose
    {
        get; init;
    }
}

internal sealed class RouteDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary
    {
        get; init;
    }

    [JsonPropertyName("description")]
    public string? Description
    {
        get; init;
    }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags
    {
        get; init;
    }

    [JsonPropertyName("input")]
    public RouteInputDefinition? Input
    {
        get; init;
    }

    [JsonPropertyName("output")]
    public RouteOutputDefinition? Output
    {
        get; init;
    }

    [JsonPropertyName("errors")]
    public IReadOnlyList<ModuleError>? Errors
    {
        get; init;
    }
}

internal sealed class RouteInputDefinition
{
    [JsonPropertyName("params")]
    public SchemaReference? Params
    {
        get; init;
    }

    [JsonPropertyName("query")]
    public SchemaReference? Query
    {
        get; init;
    }

    [JsonPropertyName("body")]
    public SchemaReference? Body
    {
        get; init;
    }

    [JsonPropertyName("headers")]
    public SchemaReference? Headers
    {
        get; init;
    }
}

internal sealed class RouteOutputDefinition
{
    [JsonPropertyName("body")]
    public SchemaReference? Body
    {
        get; init;
    }

    [JsonPropertyName("status")]
    public int? Status
    {
        get; init;
    }

    [JsonPropertyName("headers")]
    public SchemaReference? Headers
    {
        get; init;
    }
}

internal sealed class SchemaReference
{
    [JsonPropertyName("kind")]
    public string? Kind
    {
        get; init;
    }

    [JsonPropertyName("name")]
    public string? Name
    {
        get; init;
    }

    [JsonPropertyName("source")]
    public string? Source
    {
        get; init;
    }
}

internal sealed class ModuleError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public object? Details
    {
        get; init;
    }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId
    {
        get; init;
    }
}

internal sealed class ViewDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary
    {
        get; init;
    }

    [JsonPropertyName("description")]
    public string? Description
    {
        get; init;
    }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags
    {
        get; init;
    }

    [JsonPropertyName("params")]
    public SchemaReference? Params
    {
        get; init;
    }

    [JsonPropertyName("data")]
    public SchemaReference? Data
    {
        get; init;
    }
}

internal sealed class JobDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("schedule")]
    public string? Schedule
    {
        get; init;
    }

    [JsonPropertyName("priority")]
    public object? Priority
    {
        get; init;
    }
}

internal sealed class EventDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("payload")]
    public SchemaReference? Payload
    {
        get; init;
    }

    [JsonPropertyName("description")]
    public string? Description
    {
        get; init;
    }
}

internal sealed class ServiceDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description
    {
        get; init;
    }
}
