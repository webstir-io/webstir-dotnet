using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Middleware;

public class ApiProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiServerUrl;

    public ApiProxyMiddleware(RequestDelegate next, string apiServerUrl = "http://localhost:3001")
    {
        _next = next;
        _apiServerUrl = apiServerUrl;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        await ProxyApiRequest(context);
    }

    private async Task ProxyApiRequest(HttpContext context)
    {
        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("ApiProxy");
        
        try
        {
            var requestMessage = CreateProxyRequest(context, httpClient);
            var response = await httpClient.SendAsync(requestMessage);
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
        var targetUrl = context.Request.Path + context.Request.QueryString;
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(httpClient.BaseAddress!, targetUrl)
        };
        
        // Copy request headers
        foreach (var header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]);
            }
        }
        
        // Copy request body if present
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
        
        // Copy response headers
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        
        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        
        // Copy response body
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
    public static IApplicationBuilder UseApiProxy(this IApplicationBuilder builder, string apiServerUrl = "http://localhost:3001")
    {
        return builder.UseMiddleware<ApiProxyMiddleware>(apiServerUrl);
    }
}