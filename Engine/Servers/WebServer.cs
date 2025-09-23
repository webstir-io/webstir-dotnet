using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

using Engine.Bridge.Frontend;
using Engine.Extensions;
using Engine.Middleware;
using Engine.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Servers;

public class WebServer(IOptions<AppSettings> options, ILogger<WebServer> logger)
{
    private readonly List<HttpContext> _sseClients = [];
    private WebApplication? _app;

    private const string NoCache = "no-cache, no-store, must-revalidate";
    private const string NoCacheMustRevalidate = "no-cache, must-revalidate";
    private const string LongCache = "public, max-age=31536000, immutable";
    private const string PragmaNoCache = "no-cache";
    private const string ExpiresZero = "0";

    private const string SseRoute = "/sse";
    private const string ApiRoute = "/api";
    private const string HomeRoute = "/home";

    private static bool IsStaticAsset(string path) =>
        path.EndsWith(FileExtensions.Css, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Js, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Png, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Jpg, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Jpeg, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Gif, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Svg, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Webp, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Ico, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Woff, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Woff2, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Ttf, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Otf, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Eot, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Mp3, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.M4a, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Wav, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Ogg, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Mp4, StringComparison.OrdinalIgnoreCase) || path.EndsWith(FileExtensions.Webm, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(FileExtensions.Mov, StringComparison.OrdinalIgnoreCase);

    public async Task StartAsync(AppWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        FrontendResolution resolution = await ResolveFrontendAsync(workspace, cancellationToken);

        string frontendRoot = resolution.BuildPath;
        if (!frontendRoot.Exists() && resolution.Manifest is { } manifest)
        {
            string distPath = manifest.Paths.Dist.Frontend;
            if (distPath.Exists())
            {
                frontendRoot = distPath;
                logger.LogDebug("Using dist frontend root at {DistPath} for web server.", distPath);
            }
        }

        if (!frontendRoot.Exists())
        {
            logger.LogWarning("Frontend build path does not exist at {FrontendBuildPath}. Skipping web server.", frontendRoot);
            return;
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = frontendRoot
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls(options.Value.WebServerUrl);
        ConfigureServices(builder.Services);

        _app = builder.Build();
        ConfigureMiddleware(_app, frontendRoot, resolution.Manifest);

        await _app.StartAsync(cancellationToken);
        logger.LogInformation("Web server running at {WebServerUrl}", options.Value.WebServerUrl);
    }

    private async Task<FrontendResolution> ResolveFrontendAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            FrontendManifest manifest = await FrontendManifestLoader.LoadAsync(workspace, cancellationToken);
            return new FrontendResolution(manifest.Paths.Build.Frontend, manifest);
        }
        catch (FileNotFoundException)
        {
            logger.LogDebug(
                "Frontend manifest not found at {ManifestPath}; using AppWorkspace build path.",
                workspace.FrontendManifestPath);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                ex,
                "Frontend manifest invalid at {ManifestPath}; using AppWorkspace build path.",
                workspace.FrontendManifestPath);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Frontend manifest parse error at {ManifestPath}; using AppWorkspace build path.",
                workspace.FrontendManifestPath);
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex,
                "Unable to read frontend manifest at {ManifestPath}; using AppWorkspace build path.",
                workspace.FrontendManifestPath);
        }

        return new FrontendResolution(workspace.FrontendBuildPath, null);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        byte[] shutdownMessage = Encoding.UTF8.GetBytes("data: shutdown\n\n");
        Task[] tasks = [.. _sseClients.Select(async client =>
        {
            try
            {
                await client.Response.Body.WriteAsync(shutdownMessage, cancellationToken);
                await client.Response.Body.FlushAsync(cancellationToken);
            }
            catch { }
        })];
        await Task.WhenAll(tasks);

        foreach (HttpContext client in _sseClients.ToList())
        {
            try
            {
                client.Abort();
            }
            catch { }
        }
        _sseClients.Clear();

        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
        }
    }

    public async Task UpdateClientsAsync(CancellationToken cancellationToken = default)
    {
        string message = "data: reload\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(message);

        foreach (HttpContext client in _sseClients.ToList())
        {
            try
            {
                await client.Response.Body.WriteAsync(bytes, cancellationToken);
                await client.Response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
                _sseClients.Remove(client);
            }
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();
        services.AddDirectoryBrowser();
        services.AddHttpClient("ApiProxy", client =>
        {
            client.BaseAddress = new Uri(options.Value.ApiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private void ConfigureMiddleware(WebApplication app, string webRootPath, FrontendManifest? manifest)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<ClientErrorMiddleware>();
        app.Use(HandleServerSentEvents);
        app.UseMiddleware<ApiProxyMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        bool enablePrecompression = manifest?.Features.Precompression ?? true;
        if (enablePrecompression)
        {
            app.UseMiddleware<PrecompressionMiddleware>();
        }
        else
        {
            logger.LogDebug("Precompression disabled via frontend manifest; skipping middleware.");
        }
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
        if (context.Request.Path == SseRoute)
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

        if (WebServerRegexPatterns.ContentHashedAssetPattern().IsMatch(path))
        {
            context.Response.Headers.CacheControl = LongCache;
        }
        else if (path.EndsWith(Files.RefreshJs, StringComparison.Ordinal))
        {
            context.Response.Headers.CacheControl = NoCache;
            context.Response.Headers.Pragma = PragmaNoCache;
            context.Response.Headers.Expires = ExpiresZero;
        }
        else if (path.EndsWith(FileExtensions.Html, StringComparison.OrdinalIgnoreCase) || !path.Contains('.'))
        {
            context.Response.Headers.CacheControl = NoCache;
            context.Response.Headers.Pragma = PragmaNoCache;
            context.Response.Headers.Expires = ExpiresZero;
        }
        else if (IsStaticAsset(path))
        {
            context.Response.Headers.CacheControl = NoCacheMustRevalidate;
        }
    }

    private async Task RewriteCleanUrls(HttpContext context, Func<Task> next)
    {
        string? path = context.Request.Path.Value;

        if (!string.IsNullOrEmpty(path))
        {
            if (path == "/")
                path = HomeRoute;

            if (path.StartsWith("/" + Files.Index + ".", StringComparison.Ordinal) && !path.StartsWith("/" + Files.IndexHtml, StringComparison.Ordinal))
            {
                context.Request.Path = $"/{Folders.Pages}/{Folders.Home}{path}";
            }
            else if (!path.Contains('.') &&
                !path.StartsWith("/" + Folders.Images, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Fonts, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Media, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Pages, StringComparison.Ordinal) &&
                !path.StartsWith(ApiRoute, StringComparison.Ordinal) &&
                !path.StartsWith(SseRoute, StringComparison.Ordinal))
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

internal static partial class WebServerRegexPatterns
{
    // Matches content-hashed assets with 8-64 character hash before file extension
    // Example: styles.abc123def456.css, script.1234567890abcdef.js
    [GeneratedRegex(@"\.[a-f0-9]{8,64}\.(css|js|png|jpg|jpeg|gif|svg|webp|ico|woff2?|ttf|otf|eot|mp3|m4a|wav|ogg|mp4|webm|mov)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    public static partial Regex ContentHashedAssetPattern();
}

internal readonly record struct FrontendResolution(string BuildPath, FrontendManifest? Manifest);
