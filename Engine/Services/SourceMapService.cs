using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Engine.Services;

public interface ISourceMapService
{
    bool IsAuthorized(HttpContext context);
}

public sealed class SourceMapService(IOptions<AppSettings> options) : ISourceMapService
{
    private readonly AppSettings _settings = options.Value;
    private const string HeaderName = "X-SourceMap-Token";

    public bool IsAuthorized(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? token = _settings.SourceMapToken;
        if (string.IsNullOrEmpty(token))
        {
            // No token configured: do not restrict (dev-friendly default)
            return true;
        }

        string provided = context.Request.Headers[HeaderName].ToString();
        return !string.IsNullOrEmpty(provided) && string.Equals(provided, token, StringComparison.Ordinal);
    }
}
