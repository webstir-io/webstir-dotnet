using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Engine.Bridge.Backend;
using Engine.Bridge.Frontend;
using Engine.Bridge.Module;
using Engine.Extensions;
using Engine.Middleware;
using Engine.Models;
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
    private readonly object _sseClientsLock = new();
    private WebApplication? _app;
    private readonly object _reloadLock = new();
    private CancellationTokenSource? _pendingReloadCts;
    private Task _pendingReloadTask = Task.CompletedTask;

    private static readonly JsonSerializerOptions HotUpdateSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions BackendManifestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private const string NoCache = "no-cache, no-store, must-revalidate";
    private const string NoCacheMustRevalidate = "no-cache, must-revalidate";
    private const string LongCache = "public, max-age=31536000, immutable";
    private const string PragmaNoCache = "no-cache";
    private const string ExpiresZero = "0";

    private const string SseRoute = "/sse";
    private const string ApiRoute = "/api";
    private const string HomeRoute = "/home";

    private static readonly TimeSpan ReloadDebounceInterval = TimeSpan.FromMilliseconds(200);

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
        builder.WebHost.UseUrls(options.Value.WebServerListenUrl);
        ConfigureServices(builder.Services);

        _app = builder.Build();
        ConfigureMiddleware(_app, frontendRoot, resolution.Manifest, workspace);

        await _app.StartAsync(cancellationToken);
        logger.LogInformation("Web server listening on {WebServerUrl}", options.Value.WebServerListenUrl);
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
        CancelPendingReload();

        await BroadcastAsync("data: shutdown\n\n", cancellationToken);

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
        CancellationTokenSource cts;
        lock (_reloadLock)
        {
            _pendingReloadCts?.Cancel();
            _pendingReloadCts?.Dispose();
            _pendingReloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts = _pendingReloadCts;
            _pendingReloadTask = SendReloadAfterDelayAsync(cts);
        }

        await _pendingReloadTask;
    }

    public async Task PublishStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        await SendSseEventAsync("status", status, cancellationToken);
    }

    public async Task PublishHotUpdateAsync(FrontendHotUpdate hotUpdate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hotUpdate);

        if (hotUpdate.RequiresReload)
        {
            throw new InvalidOperationException("Hot updates that require reload must be handled via UpdateClientsAsync().");
        }

        object payload = new
        {
            ChangedFile = string.IsNullOrWhiteSpace(hotUpdate.ChangedFile) ? null : hotUpdate.ChangedFile,
            Modules = hotUpdate.Modules.Select(asset => new
            {
                asset.Type,
                asset.Url,
                asset.RelativePath
            }),
            Styles = hotUpdate.Styles.Select(asset => new
            {
                asset.Type,
                asset.Url,
                asset.RelativePath
            }),
            hotUpdate.FallbackReasons,
            Stats = hotUpdate.Stats is null
                ? null
                : new
                {
                    hotUpdate.Stats.HotUpdates,
                    hotUpdate.Stats.ReloadFallbacks
                }
        };

        string serialized = JsonSerializer.Serialize(payload, HotUpdateSerializerOptions);
        await SendSseEventAsync("hmr", serialized, cancellationToken);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ErrorTrackingService>();
        services.AddDirectoryBrowser();
        services.AddHttpClient("ApiProxy", client =>
        {
            client.BaseAddress = new Uri(options.Value.ApiServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private void ConfigureMiddleware(WebApplication app, string webRootPath, FrontendManifest? manifest, AppWorkspace workspace)
    {
        MapBackendManifestEndpoint(app, workspace);
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

    private void MapBackendManifestEndpoint(WebApplication app, AppWorkspace workspace)
    {
        app.MapGet("/__webstir/backend/manifest", async context =>
        {
            try
            {
                ModuleBuildManifest manifest = await BackendManifestLoader.LoadAsync(workspace, context.RequestAborted);
                ModuleRuntimeManifest? module = manifest.Module;

                object payload = module is null
                    ? new
                    {
                        module = (object?)null,
                        manifest.EntryPoints,
                        manifest.StaticAssets,
                        manifest.Diagnostics
                    }
                    : new
                    {
                        Module = new
                        {
                            module.Name,
                            module.Version,
                            module.Kind,
                            module.Capabilities,
                            Routes = module.Routes ?? Array.Empty<RouteDefinition>(),
                            Views = module.Views ?? Array.Empty<ViewDefinition>()
                        },
                        manifest.EntryPoints,
                        manifest.StaticAssets,
                        manifest.Diagnostics
                    };

                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    payload,
                    BackendManifestSerializerOptions,
                    context.RequestAborted);
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load backend manifest for inspection.");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    new
                    {
                        error = "backend manifest unavailable"
                    },
                    BackendManifestSerializerOptions,
                    context.RequestAborted);
            }
        }).WithDisplayName("Webstir Backend Manifest");
    }

    private async Task SendReloadAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(ReloadDebounceInterval, cts.Token);
            await BroadcastAsync("data: reload\n\n", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled; no reload needed.
        }
        finally
        {
            lock (_reloadLock)
            {
                if (_pendingReloadCts == cts)
                {
                    _pendingReloadCts = null;
                    _pendingReloadTask = Task.CompletedTask;
                }
            }

            cts.Dispose();
        }
    }

    private async Task BroadcastAsync(string message, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);

        HttpContext[] clients;
        lock (_sseClientsLock)
        {
            clients = _sseClients.ToArray();
        }

        foreach (HttpContext client in clients)
        {
            try
            {
                await client.Response.Body.WriteAsync(bytes, cancellationToken);
                await client.Response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
                lock (_sseClientsLock)
                {
                    _sseClients.Remove(client);
                }
            }
        }
    }

    private Task SendSseEventAsync(string eventName, string? data, CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        builder.Append("event: ").Append(eventName).Append('\n');
        if (!string.IsNullOrEmpty(data))
        {
            builder.Append("data: ").Append(data).Append('\n');
        }

        builder.Append('\n');
        return BroadcastAsync(builder.ToString(), cancellationToken);
    }

    private void CancelPendingReload()
    {
        lock (_reloadLock)
        {
            if (_pendingReloadCts is null)
            {
                return;
            }

            _pendingReloadCts.Cancel();
            _pendingReloadCts.Dispose();
            _pendingReloadCts = null;
            _pendingReloadTask = Task.CompletedTask;
        }
    }

    private async Task HandleServerSentEvents(HttpContext context, Func<Task> next)
    {
        if (context.Request.Path == SseRoute)
        {
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");

            lock (_sseClientsLock)
            {
                _sseClients.Add(context);
            }

            try
            {
                await context.Response.Body.FlushAsync();

                TaskCompletionSource tcs = new();
                context.RequestAborted.Register(tcs.SetResult);
                await tcs.Task;
            }
            finally
            {
                lock (_sseClientsLock)
                {
                    _sseClients.Remove(context);
                }
            }
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
        else if (path.EndsWith(Files.RefreshJs, StringComparison.Ordinal)
                 || path.EndsWith(Files.HmrJs, StringComparison.Ordinal))
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
            if (path.StartsWith("/__webstir", StringComparison.Ordinal))
            {
                await next();
                return;
            }

            if (path == "/")
                path = HomeRoute;

            if (path.StartsWith("/" + Files.Index + ".", StringComparison.Ordinal) && !path.StartsWith("/" + Files.IndexHtml, StringComparison.Ordinal))
            {
                context.Request.Path = $"/{Folders.Pages}/{Folders.Home}{path}";
            }
            else if (path.EndsWith($"/{Files.Index}{FileExtensions.Js}", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith($"/{Files.Index}{FileExtensions.Css}", StringComparison.OrdinalIgnoreCase))
            {
                string[] segments = path.Trim('/').Split('/');
                if (segments.Length == 2)
                {
                    string pageName = segments[0];
                    string fileName = segments[1];
                    string candidate = $"/{Folders.Pages}/{pageName}/{fileName}";

                    string webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                    string fullPath = Path.Combine(webRoot, candidate.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                    {
                        context.Request.Path = candidate;
                    }
                }
            }
            else if (!path.Contains('.') &&
                !path.StartsWith("/" + Folders.Images, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Fonts, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Media, StringComparison.Ordinal) &&
                !path.StartsWith("/" + Folders.Pages, StringComparison.Ordinal) &&
                !path.StartsWith(ApiRoute, StringComparison.Ordinal) &&
                !path.StartsWith(SseRoute, StringComparison.Ordinal))
            {
                string pageName = path.Trim('/');
                string indexPath = $"/{Folders.Pages}/{pageName}/{Files.IndexHtml}";

                string webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                string fullPath = Path.Combine(webRoot, indexPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    context.Request.Path = indexPath;
                else if (pageName.StartsWith("docs/", StringComparison.Ordinal))
                {
                    // Fuzzy match doc slug anywhere under pages/docs
                    string slug = Path.GetFileName(pageName);
                    string docsRoot = Path.Combine(webRoot, Folders.Pages, "docs");
                    if (Directory.Exists(docsRoot))
                    {
                        string? match = Directory.EnumerateFiles(docsRoot, $"{slug}.html", SearchOption.AllDirectories).FirstOrDefault();
                        if (match is not null)
                        {
                            string relative = "/" + Path.GetRelativePath(webRoot, match).Replace('\\', '/');
                            context.Request.Path = relative;
                        }
                    }
                }
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
