using System;
using System.Threading.Tasks;
using Engine.Bridge.Frontend;
using Microsoft.AspNetCore.Http;

namespace Engine.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "SAMEORIGIN";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.XXSSProtection = "0";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            headers.ContentSecurityPolicy = ContentSecurityPolicy.BuildDefaultPolicy(isDevelopment: true);
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
