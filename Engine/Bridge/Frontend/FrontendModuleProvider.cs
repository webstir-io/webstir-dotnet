using System;

namespace Engine.Bridge.Frontend;

internal sealed record FrontendModuleProvider
{
    public FrontendModuleProvider(string id)
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
