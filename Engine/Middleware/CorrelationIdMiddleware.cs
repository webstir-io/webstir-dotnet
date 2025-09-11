using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Engine.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = GetOrCreateCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        IDisposable? scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
        try
        {
            await _next(context);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        string header = context.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(header))
        {
            return header;
        }

        return Guid.NewGuid().ToString("n");
    }
}
