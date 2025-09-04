using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Middleware;

public class ApiProxyMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        await ProxyApiRequest(context);
    }

    private static async Task ProxyApiRequest(HttpContext context)
    {
        IHttpClientFactory httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient("ApiProxy");

        try
        {
            HttpRequestMessage requestMessage = CreateProxyRequest(context, httpClient);
            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            await CopyProxyResponse(context, response);
        }
        catch (HttpRequestException ex)
        {
            await WriteErrorResponse(context, 503, $"API server unavailable: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            await WriteErrorResponse(context, 504, "API request timeout");
        }
    }

    private static HttpRequestMessage CreateProxyRequest(HttpContext context, HttpClient httpClient)
    {
        string targetUrl = context.Request.Path + context.Request.QueryString;
        HttpRequestMessage requestMessage = new()
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(httpClient.BaseAddress!, targetUrl)
        };

        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]);
            }
        }

        if (context.Request.ContentLength > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
            requestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                context.Request.ContentType ?? "application/json");
        }

        return requestMessage;
    }

    private static async Task CopyProxyResponse(HttpContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        await response.Content.CopyToAsync(context.Response.Body);
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string errorMessage)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"error\": \"{errorMessage}\"}}");
    }
}

public static class ApiProxyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiProxy(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ApiProxyMiddleware>();
}
