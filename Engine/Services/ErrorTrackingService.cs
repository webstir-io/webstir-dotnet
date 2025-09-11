using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

public interface IErrorTrackingService
{
    void Capture(Exception exception, HttpContext context, string correlationId);
}

public sealed class ErrorTrackingService(ILogger<ErrorTrackingService> logger) : IErrorTrackingService
{
    private readonly ILogger<ErrorTrackingService> _logger = logger;

    public void Capture(Exception exception, HttpContext context, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = context.Request.Path.Value ?? string.Empty;
        string method = context.Request.Method;
        string userAgent = context.Request.Headers["User-Agent"].ToString();
        _logger.LogError(exception, "Captured error: {Method} {Path} (CorrelationId: {CorrelationId}) UA={UserAgent}", method, path, correlationId, userAgent);

        // Integration point: wire up Sentry/Rollbar/etc. using env vars or options.
        // Intentionally left as no-op to keep defaults lightweight.
    }
}
