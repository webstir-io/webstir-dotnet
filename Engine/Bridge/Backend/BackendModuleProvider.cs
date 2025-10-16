using System;

namespace Engine.Bridge.Backend;

internal sealed record BackendModuleProvider
{
    public BackendModuleProvider(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Provider id cannot be null or whitespace.", nameof(id));
        }

        Id = id;
    }

    public string Id
    {
        get;
    }
}
