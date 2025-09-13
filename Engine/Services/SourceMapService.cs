using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Engine.Services;

public interface ISourceMapService
{
    bool IsAuthorized(HttpContext context);
}

public sealed class SourceMapService : ISourceMapService
{

    public bool IsAuthorized(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return true;
    }
}
