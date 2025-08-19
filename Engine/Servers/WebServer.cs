using System.Text;
using Engine.Extensions;
using Engine.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Engine.Servers;

public class WebServer(AppSettings settings)
{
    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _app;

    public async Task StartAsync(AppContext context)
    {
        if (!context.ClientBuildPath.Exists())
        {
            Console.WriteLine("Client build path does not exist. Skipping web server.");
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = context.ClientBuildPath
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning); // Only show warnings and errors
        builder.WebHost.UseUrls(settings.WebServerUrl);
        ConfigureServices(builder.Services);
        
        _app = builder.Build();
        ConfigureMiddleware(_app, context.ClientBuildPath);
        
        await _app.StartAsync();
        Console.WriteLine($"Web server running at {settings.WebServerUrl}");
    }

    public async Task StopAsync()
    {
        var shutdownMessage = Encoding.UTF8.GetBytes("data: shutdown\n\n");
        var tasks = _sseClients.Select(async client =>
        {
            try
            {
                await client.Response.Body.WriteAsync(shutdownMessage);
                await client.Response.Body.FlushAsync();
            }
            catch { }
        });
        await Task.WhenAll(tasks);
        
        foreach (var client in _sseClients.ToList())
        {
            try { client.Abort(); }
            catch { }
        }
        _sseClients.Clear();
        
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }

    public async Task UpdateClientsAsync()
    {
        var message = "data: reload\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        
        foreach (var client in _sseClients.ToList())
        {
            try
            {
                await client.Response.Body.WriteAsync(bytes);
                await client.Response.Body.FlushAsync();
            }
            catch
            {
                _sseClients.Remove(client);
            }
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddDirectoryBrowser();
        services.AddHttpClient("ApiProxy", client =>
        {
            client.BaseAddress = new Uri(settings.ApiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private void ConfigureMiddleware(WebApplication app, string webRootPath)
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
            FileProvider = new PhysicalFileProvider(webRootPath),
            EnableDirectoryBrowsing = false
        });
    }

    private async Task HandleServerSentEvents(HttpContext context, Func<Task> next)
    {
        if (context.Request.Path == "/sse")
        {
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            
            _sseClients.Add(context);
            
            await context.Response.Body.FlushAsync();
            
            var tcs = new TaskCompletionSource();
            context.RequestAborted.Register(() => tcs.SetResult());
            await tcs.Task;
            
            _sseClients.Remove(context);
        }
        else
        {
            await next();
        }
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
                !path.StartsWith("/sse"))
            {
                var pageName = path.TrimStart('/');
                var indexPath = $"/pages/{pageName}/index.html";
                
                var webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                var fullPath = Path.Combine(webRoot, indexPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    context.Request.Path = indexPath;
            }
        }
        
        await next();
    }
}