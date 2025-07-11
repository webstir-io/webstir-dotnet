using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using CLI.Interfaces;
using CLI.Middleware;

namespace CLI.Services;

public class WebServer() : IWebServer
{
    private const string _apiServerUrl = "http://localhost:3001";

    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _app;
    private string _webRootPath = "build";

    public bool IsRunning => _app != null;

    public async Task StartAsync()
    {
        // Check for new structure first, fallback to legacy
        if (Directories.ClientBuildDirectory.Exists)
        {
            _webRootPath = Directories.ClientBuildDirectory.FullName;
        }
        else if (Directories.BuildDirectory.Exists)
        {
            _webRootPath = Directories.BuildDirectory.FullName;
        }
        else
        {
            throw new DirectoryNotFoundException($"No valid webroot found. Expected '{Directories.ClientBuildDirectory.FullName}' or '{Directories.BuildDirectory.FullName}'.");
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

        var app = builder.Build();
        
        // Configure middleware pipeline
        app.Use(HandleServerSentEvents);

        // Add API proxy middleware
        app.UseMiddleware<ApiProxyMiddleware>(_apiServerUrl);

        // Configure default files to look for index.html
        var defaultFilesOptions = new DefaultFilesOptions();
        defaultFilesOptions.DefaultFileNames.Clear();
        defaultFilesOptions.DefaultFileNames.Add("index.html");
        app.UseDefaultFiles(defaultFilesOptions);
        
        app.UseStaticFiles();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(_webRootPath),
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