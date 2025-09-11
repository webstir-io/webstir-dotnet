using System;
using System.Threading.Tasks;

using Engine.Services;

using Microsoft.AspNetCore.Http;

namespace Engine.Middleware;

public sealed class SourceMapMiddleware(RequestDelegate next, ISourceMapService sourceMapService)
{
    private readonly RequestDelegate _next = next;
    private readonly ISourceMapService _sourceMapService = sourceMapService;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path) && path.EndsWith(FileExtensions.Map, StringComparison.OrdinalIgnoreCase))
        {
            if (!_sourceMapService.IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }
}

