using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Engine.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Engine.Middleware;

public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger,
    ErrorTrackingService errorTracking)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger = logger;
    private readonly ErrorTrackingService _errorTracking = errorTracking;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);

            if (!context.Response.HasStarted && context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await WriteErrorPageAsync(context, StatusCodes.Status404NotFound);
            }
        }
        catch (Exception ex)
        {
            string correlationId = GetCorrelationId(context);
            _logger.LogError(ex, "Unhandled exception (CorrelationId: {CorrelationId})", correlationId);
            _errorTracking.Capture(ex, context, correlationId);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                await WriteErrorPageAsync(context, StatusCodes.Status500InternalServerError);
            }
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        return context.Items.TryGetValue("X-Correlation-ID", out object? value) && value is string id && !string.IsNullOrEmpty(id)
            ? id
            : string.Empty;
    }

    private static async Task WriteErrorPageAsync(HttpContext context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";

        string html = LoadEmbeddedErrorTemplate(statusCode) ?? DefaultHtml(statusCode);
        byte[] bytes = Encoding.UTF8.GetBytes(html);
        await context.Response.Body.WriteAsync(bytes);
    }

    private static string? LoadEmbeddedErrorTemplate(int statusCode)
    {
        string file = statusCode == StatusCodes.Status404NotFound ? "404.html" : "500.html";
        string resourceName = $"Engine.Resources.Errors.{file}";
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string DefaultHtml(int statusCode)
    {
        // Try to load the default template
        string? template = LoadDefaultTemplate();
        if (template == null)
        {
            // Fallback to minimal inline HTML if template loading fails
            return GetFallbackHtml(statusCode);
        }

        string title = statusCode == StatusCodes.Status404NotFound ? "Page Not Found" : "Something went wrong";
        string message = statusCode == StatusCodes.Status404NotFound
            ? "The page you requested could not be found."
            : "An unexpected error occurred. Please try again later.";

        return template
            .Replace("{{TITLE}}", title)
            .Replace("{{MESSAGE}}", message)
            .Replace("{{STATUS}}", statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string? LoadDefaultTemplate()
    {
        string resourceName = "Engine.Resources.Errors.default.html";
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string GetFallbackHtml(int statusCode)
    {
        string title = statusCode == StatusCodes.Status404NotFound ? "Page Not Found" : "Something went wrong";
        string message = statusCode == StatusCodes.Status404NotFound
            ? "The page you requested could not be found."
            : "An unexpected error occurred. Please try again later.";

        return $"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{title}</title>
            </head>
            <body>
                <h1>{title}</h1>
                <p>{message}</p>
                <p>Status code: {statusCode}</p>
            </body>
            </html>
            """;
    }
}
