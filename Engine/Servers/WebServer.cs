using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Engine.Interfaces;
using Engine.Middleware;

namespace Engine.Servers;

public class WebServer(App _app) : IWebServer
{
    private const string _apiServerUrl = "http://localhost:3001";

    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _webApp;
    private string _webRootPath = "build";

    public bool IsRunning => _webApp != null;

    public async Task StartAsync()
    {
        // Check for new structure first, fallback to legacy
        if (_app.ClientBuildDir.Exists)
        {
            _webRootPath = _app.ClientBuildDir.FullName;
        }
        else if (_app.BuildDir.Exists)
        {
            _webRootPath = _app.BuildDir.FullName;
        }
        else
        {
            throw new DirectoryNotFoundException($"No valid webroot found. Expected '{_app.ClientBuildDir.FullName}' or '{_app.BuildDir.FullName}'.");
        }

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

        _webApp = builder.Build();
        
        // Configure middleware pipeline
        _webApp.Use(HandleServerSentEvents);

        // Add API proxy middleware
        _webApp.UseMiddleware<ApiProxyMiddleware>(_apiServerUrl);

        // Configure default files to look for index.html
        var defaultFilesOptions = new DefaultFilesOptions();
        defaultFilesOptions.DefaultFileNames.Clear();
        defaultFilesOptions.DefaultFileNames.Add("index.html");
        _webApp.UseDefaultFiles(defaultFilesOptions);

        _webApp.UseStaticFiles();
        _webApp.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(_webRootPath),
            EnableDirectoryBrowsing = true
        });
        
        // Start the server asynchronously without blocking
        _ = Task.Run(async () => 
        {
            try
            {
                await _webApp.RunAsync("http://0.0.0.0:8088");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        });
        
        // Give the server a moment to start
        await Task.Delay(100);
    }

    public async Task StopAsync()
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
        if (_webApp != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _webApp.StopAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // If graceful shutdown times out, that's okay
                Console.WriteLine("Server shutdown timed out, forcing exit");
            }
        }
    }

    public async Task UpdateClientsAsync()
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

}