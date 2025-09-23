using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Engine.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Engine.Middleware;

public sealed class ClientErrorMiddleware(RequestDelegate next, ILogger<ClientErrorMiddleware> logger, ErrorTrackingService errorTracking)
{
    private const string Route = "/client-errors";

    private readonly RequestDelegate _next = next;
    private readonly ILogger<ClientErrorMiddleware> _logger = logger;
    private readonly ErrorTrackingService _errorTracking = errorTracking;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PathString path = context.Request.Path;
        if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.HasValue && path.Value!.Equals(Route, StringComparison.Ordinal))
        {
            await HandleClientErrorAsync(context);
            return;
        }

        await _next(context);
    }

    private async Task HandleClientErrorAsync(HttpContext context)
    {
        try
        {
            // Require JSON
            string? contentType = context.Request.ContentType;
            if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            // Enforce payload size limit (32KB)
            const int MaxPayloadBytes = 32 * 1024;
            long? contentLength = context.Request.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxPayloadBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            byte[]? body = await ReadBodyLimitedAsync(context.Request.Body, MaxPayloadBytes);
            if (body == null)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            ClientErrorPayload? payload = JsonSerializer.Deserialize<ClientErrorPayload>(body);
            if (payload == null)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return; // ignore invalid bodies quietly
            }

            string correlationId = !string.IsNullOrEmpty(payload.correlationId)
                ? payload.correlationId!
                : GetCorrelationId(context);

            ClientReportedException ex = new(payload.message ?? "Client error")
            {
                Type = payload.type,
                FileName = payload.filename,
                Line = payload.lineno,
                Column = payload.colno,
                Stack = payload.stack,
                PageUrl = payload.pageUrl,
                UserAgent = payload.userAgent,
                Timestamp = payload.timestamp
            };

            _logger.LogError("Client error: {Message} at {File}:{Line}:{Column} (CorrelationId: {CorrelationId})",
                ex.Message, ex.FileName, ex.Line, ex.Column, correlationId);

            _errorTracking.Capture(ex, context, correlationId);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process client error report");
        }
    }

    private static async Task<byte[]?> ReadBodyLimitedAsync(Stream stream, int maxBytes)
    {
        if (stream == Stream.Null)
        {
            return Array.Empty<byte>();
        }

        using MemoryStream ms = new(Math.Min(8192, maxBytes));
        byte[] buffer = new byte[8192];
        int total = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, maxBytes - total));
            if (read <= 0)
            {
                break;
            }
            total += read;
            if (total > maxBytes)
            {
                return null; // too large
            }
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }

    private static string GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue("X-Correlation-ID", out object? value) && value is string id && !string.IsNullOrEmpty(id))
        {
            return id;
        }
        string header = context.Request.Headers["X-Correlation-ID"].ToString();
        return !string.IsNullOrEmpty(header) ? header : string.Empty;
    }

    private sealed class ClientErrorPayload
    {
        public string? type
        {
            get; set;
        }
        public string? message
        {
            get; set;
        }
        public string? stack
        {
            get; set;
        }
        public string? filename
        {
            get; set;
        }
        public int? lineno
        {
            get; set;
        }
        public int? colno
        {
            get; set;
        }
        public string? pageUrl
        {
            get; set;
        }
        public string? userAgent
        {
            get; set;
        }
        public string? timestamp
        {
            get; set;
        }
        public string? correlationId
        {
            get; set;
        }
    }
}

public sealed class ClientReportedException(string message) : Exception(message)
{
    public string? Type
    {
        get; init;
    }
    public string? FileName
    {
        get; init;
    }
    public int? Line
    {
        get; init;
    }
    public int? Column
    {
        get; init;
    }
    public string? Stack
    {
        get; init;
    }
    public string? PageUrl
    {
        get; init;
    }
    public string? UserAgent
    {
        get; init;
    }
    public string? Timestamp
    {
        get; init;
    }

    public override string? StackTrace => Stack ?? base.StackTrace;
}
