using System.Text;
using Engine.Extensions;
using Engine.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Engine.Servers;

public class WebServer : IWebServer
{
    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _webApp;
    private string _webRootPath = Folders.Build;

    public bool IsRunning => _webApp != null;

    public Task StartAsync(AppContext? context = null)
    {
        ValidateWebRoot(context);
        
        var builder = CreateWebApplicationBuilder();
        ConfigureServices(builder.Services);
        
        _webApp = builder.Build();
        ConfigureMiddleware(_webApp);
        
        Task.Run(RunServerAsync);
        
        return Task.CompletedTask;
    }

    private void ValidateWebRoot(AppContext? context)
    {
        if (context?.ClientBuildPath.Exists() == true)
            _webRootPath = context.ClientBuildPath;
        else
            throw new DirectoryNotFoundException($"No valid webroot found. Expected '{context?.ClientBuildPath}'.");
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
        services.AddDirectoryBrowser();
        services.AddSingleton<AppSettings>();
        services.AddHttpClient("ApiProxy", (serviceProvider, client) =>
        {
            var appSettings = serviceProvider.GetRequiredService<AppSettings>();
            client.BaseAddress = new Uri(appSettings.ApiServerUrl);
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
        
        if (!string.IsNullOrEmpty(path))
        {
            if (path == "/")
                path = "/home";
            
            if (path.StartsWith("/index.") && !path.StartsWith("/index.html"))
            {
                context.Request.Path = $"/pages/home{path}";
            }
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
                    context.Request.Path = indexPath;
            }
        }
        
        await next();
    }

    private async Task RunServerAsync()
    {
        var appSettings = _webApp!.Services.GetRequiredService<AppSettings>();
        await _webApp.RunAsync(appSettings.WebServerUrl);
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

            var message = "data: connected\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await context.Response.Body.WriteAsync(bytes);
            await context.Response.Body.FlushAsync();

            try
            {
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