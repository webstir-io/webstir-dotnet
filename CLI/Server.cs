using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CLI;

public class Server()
{
    private const string _webRootPath = "build";
    private const string _apiServerUrl = "http://localhost:3001";

    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _app;

    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions 
        { 
            WebRootPath = _webRootPath
        });

        // Configure host shutdown timeout
        builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(5));
        builder.Services.AddDirectoryBrowser();
        
        // Add HttpClient for API proxy
        builder.Services.AddHttpClient("ApiProxy", client =>
        {
            client.BaseAddress = new Uri(_apiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var app = builder.Build();
        
        // Configure middleware pipeline
        app.Use(HandleServerSentEvents);

        // Add proxy middleware for API requests
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"),
            proxyApp => proxyApp.Run(ProxyApiRequest));

        var rewriteOptions = new RewriteOptions().AddRewrite(@"^([\w\-/]+)$", "$1.html", skipRemainingRules: true);
        app.UseRewriter(rewriteOptions);
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, _webRootPath)),
            EnableDirectoryBrowsing = true
        });

        _app = app;
        
        // Start the server asynchronously without blocking
        _ = Task.Run(async () => 
        {
            try
            {
                await _app.RunAsync("http://0.0.0.0:8088");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        });
        
        // Give the server a moment to start
        await Task.Delay(100);
    }

    public async Task Stop()
    {
        // Forcefully abort all SSE connections
        foreach (var context in _sseClients.ToList())
        {
            try
            {
                // Abort the connection immediately
                context.Abort();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }
        _sseClients.Clear();
        
        // Now stop the app with a short timeout
        if (_app != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _app.StopAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // If graceful shutdown times out, that's okay
                Console.WriteLine("Server shutdown timed out, forcing exit");
            }
        }
    }

    public async Task Update()
    {
        var deadClients = new List<HttpContext>();
        
        foreach (var context in _sseClients.ToList())
        {
            try
            {
                var message = "data: reload\n\n";
                var bytes = Encoding.UTF8.GetBytes(message);
                await context.Response.Body.WriteAsync(bytes);
                await context.Response.Body.FlushAsync();
            }
            catch
            {
                deadClients.Add(context);
            }
        }
        
        // Remove disconnected clients
        foreach (var client in deadClients)
        {
            _sseClients.Remove(client);
        }
    }

    private async Task HandleServerSentEvents(HttpContext context, Func<Task> next)
    {
        if (context.Request.Path == "/events")
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            
            _sseClients.Add(context);
            
            // Send initial connection message
            var message = "data: connected\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await context.Response.Body.WriteAsync(bytes);
            await context.Response.Body.FlushAsync();
            
            try
            {
                // Keep connection open
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - this is expected
            }
            finally
            {
                _sseClients.Remove(context);
            }
        }
        else
        {
            await next();
        }
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