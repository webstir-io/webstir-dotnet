using System.Text;
using System.Text.RegularExpressions;
using Engine.Extensions;
using Engine.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Servers;

public partial class WebServer(IOptions<AppSettings> options, ILogger<WebServer> logger)
{
    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _app;

    [GeneratedRegex(@"\.\d{10}\.(css|js|png|jpg|jpeg|gif|svg|webp|ico)$")]
    private static partial Regex TimestampedAssetPattern();

    private static bool IsStaticAsset(string path) =>
        path.EndsWith(FileExtensions.Css) || path.EndsWith(FileExtensions.Js) || 
        path.EndsWith(FileExtensions.Png) || path.EndsWith(FileExtensions.Jpg) || 
        path.EndsWith(FileExtensions.Jpeg) || path.EndsWith(FileExtensions.Gif) || 
        path.EndsWith(FileExtensions.Svg) || path.EndsWith(FileExtensions.Webp) || 
        path.EndsWith(FileExtensions.Ico);

    public async Task StartAsync(AppWorkspace workspace)
    {
        if (!workspace.ClientBuildPath.Exists())
        {
            logger.LogWarning("Client build path does not exist. Skipping web server.");
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = workspace.ClientBuildPath
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls(options.Value.WebServerUrl);
        ConfigureServices(builder.Services);
        
        _app = builder.Build();
        ConfigureMiddleware(_app, workspace.ClientBuildPath);
        
        await _app.StartAsync();
        logger.LogInformation("Web server running at {WebServerUrl}", options.Value.WebServerUrl);
    }

    public async Task StopAsync()
    {
        byte[] shutdownMessage = Encoding.UTF8.GetBytes("data: shutdown\n\n");
        Task[] tasks = [.. _sseClients.Select(async client =>
        {
            try
            {
                await client.Response.Body.WriteAsync(shutdownMessage);
                await client.Response.Body.FlushAsync();
            }
            catch { }
        })];
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
        string message = "data: reload\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        
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
            client.BaseAddress = new Uri(options.Value.ApiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private void ConfigureMiddleware(WebApplication app, string webRootPath)
    {
        app.Use(HandleServerSentEvents);
        app.UseMiddleware<ApiProxyMiddleware>();
        app.Use(SetCacheHeaders);
        app.Use(RewriteCleanUrls);

        DefaultFilesOptions defaultFilesOptions = new();
        defaultFilesOptions.DefaultFileNames.Clear();
        defaultFilesOptions.DefaultFileNames.Add(Files.IndexHtml);
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
        if (context.Request.Path == Routes.Sse)
        {
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            
            _sseClients.Add(context);
            
            await context.Response.Body.FlushAsync();
            
            TaskCompletionSource tcs = new();
            context.RequestAborted.Register(tcs.SetResult);
            await tcs.Task;
            
            _sseClients.Remove(context);
        }
        else
        {
            await next();
        }
    }

    private async Task SetCacheHeaders(HttpContext context, Func<Task> next)
    {
        await next();
        
        if (context.Response.HasStarted)
            return;
            
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        
        if (TimestampedAssetPattern().IsMatch(path))
        {
            context.Response.Headers.CacheControl = CacheHeaders.LongCache;
        }
        else if (path.EndsWith(Files.RefreshJs))
        {
            context.Response.Headers.CacheControl = CacheHeaders.NoCache;
            context.Response.Headers.Pragma = CacheHeaders.PragmaNoCache;
            context.Response.Headers.Expires = CacheHeaders.ExpiresZero;
        }
        else if (path.EndsWith(FileExtensions.Html) || !path.Contains('.'))
        {
            context.Response.Headers.CacheControl = CacheHeaders.NoCache;
            context.Response.Headers.Pragma = CacheHeaders.PragmaNoCache;
            context.Response.Headers.Expires = CacheHeaders.ExpiresZero;
        }
        else if (IsStaticAsset(path))
        {
            context.Response.Headers.CacheControl = CacheHeaders.NoCacheMustRevalidate;
        }
    }

    private async Task RewriteCleanUrls(HttpContext context, Func<Task> next)
    {
        string? path = context.Request.Path.Value;
        
        if (!string.IsNullOrEmpty(path))
        {
            if (path == "/")
                path = Routes.Home;
            
            if (path.StartsWith("/" + Files.Index + ".") && !path.StartsWith("/" + Files.IndexHtml))
            {
                context.Request.Path = $"/{Folders.Pages}/{Folders.Home}{path}";
            }
            else if (!path.Contains('.') && 
                !path.StartsWith("/" + Folders.Images) && 
                !path.StartsWith("/" + Folders.Pages) &&
                !path.StartsWith(Routes.Api) && 
                !path.StartsWith(Routes.Sse))
            {
                string pageName = path.TrimStart('/');
                string indexPath = $"/{Folders.Pages}/{pageName}/{Files.IndexHtml}";
                
                string webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                string fullPath = Path.Combine(webRoot, indexPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    context.Request.Path = indexPath;
            }
        }
        
        await next();
    }
}