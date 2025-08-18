using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Engine.Middleware;
using Engine.Extensions;

namespace Engine.Servers;

//TODO: This needs lots of work
public class WebServer : IWebServer
{
    private const string _apiServerUrl = "http://localhost:8000";

    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _webApp;
    private string _webRootPath = "build";

    public bool IsRunning => _webApp != null;

    public async Task StartAsync(AppContext? context = null)
    {
        ValidateWebRoot(context);
        
        var builder = CreateWebApplicationBuilder();
        ConfigureServices(builder.Services);
        
        _webApp = builder.Build();
        ConfigureMiddleware(_webApp);
        
        await RunServerAsync();
    }

    private void ValidateWebRoot(AppContext? context)
    {
        if (context?.ClientBuildPath.Exists() == true)
        {
            _webRootPath = context.ClientBuildPath;
        }
        else
        {
            throw new DirectoryNotFoundException($"No valid webroot found. Expected '{context?.ClientBuildPath}'.");
        }
    }

    private WebApplicationBuilder CreateWebApplicationBuilder()
    {
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = _webRootPath
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(5));
        services.AddDirectoryBrowser();
        services.AddHttpClient("ApiProxy", client =>
        {
            client.BaseAddress = new Uri(_apiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private void ConfigureMiddleware(WebApplication app)
    {
        app.Use(HandleServerSentEvents);
        app.UseMiddleware<ApiProxyMiddleware>();
        app.Use(RewriteCleanUrls);

        var defaultFilesOptions = new DefaultFilesOptions();
        defaultFilesOptions.DefaultFileNames.Clear();
        defaultFilesOptions.DefaultFileNames.Add("index.html");
        app.UseDefaultFiles(defaultFilesOptions);

        app.UseStaticFiles();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(_webRootPath),
            EnableDirectoryBrowsing = false
        });
    }

    private async Task RewriteCleanUrls(HttpContext context, Func<Task> next)
    {
        var path = context.Request.Path.Value;
        var referer = context.Request.Headers["Referer"].ToString();
        
        if (!string.IsNullOrEmpty(path))
        {
            // Map root to home
            if (path == "/")
            {
                path = "/home";
            }
            
            // Handle root-level assets that should come from home page
            if (path.StartsWith("/index.") && !path.StartsWith("/index.html"))
            {
                context.Request.Path = $"/pages/home{path}";
            }
            // Rewrite clean URLs to pages/{name}/index.html
            else if (!path.Contains('.') && 
                !path.StartsWith("/images") && 
                !path.StartsWith("/pages") &&
                !path.StartsWith("/api") && 
                !path.StartsWith("/events"))
            {
                var pageName = path.TrimStart('/');
                var indexPath = $"/pages/{pageName}/index.html";
                
                var fullPath = Path.Combine(_webRootPath, indexPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                {
                    context.Request.Path = indexPath;
                }
            }
        }
        
        await next();
    }

    private async Task RunServerAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _webApp!.RunAsync("http://0.0.0.0:8088");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        });

        // TODO: Seriously claude?!
        await Task.Delay(100);
    }

    public async Task StopAsync()
    {
        foreach (var context in _sseClients.ToList())
        {
            try
            {
                context.Abort();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }
        _sseClients.Clear();

        if (_webApp != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _webApp.StopAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
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