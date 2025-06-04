using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;

namespace CLI;

public class Server()
{
    private const string _webRootPath = "build/bin";

    private readonly List<StreamWriter> _sseClients = [];

    public void Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions 
        { 
            WebRootPath = _webRootPath
        });

        builder.Services.AddDirectoryBrowser();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/events")
            {
                context.Response.Headers.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";
                
                var response = context.Response;
                var writer = new StreamWriter(response.Body);
                
                _sseClients.Add(writer);
                
                // Send initial connection message
                await writer.WriteLineAsync("data: connected\n");
                await writer.FlushAsync();
                
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
                    _sseClients.Remove(writer);
                    writer.Dispose();
                }
            }
            else
            {
                await next();
            }
        });

        var rewriteOptions = new RewriteOptions().AddRewrite(@"^([\w\-/]+)$", "$1.html", skipRemainingRules: true);
        app.UseRewriter(rewriteOptions);
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, _webRootPath)),
            EnableDirectoryBrowsing = true
        });

        app.Run("http://0.0.0.0:8000");        
    }

    public void Stop()
    {
        foreach (var client in _sseClients)
        {
            client.Dispose();
        }
        _sseClients.Clear();
    }

    public async Task Update()
    {
        var deadClients = new List<StreamWriter>();
        
        foreach (var client in _sseClients)
        {
            try
            {
                await client.WriteLineAsync("data: reload\n");
                await client.FlushAsync();
            }
            catch
            {
                deadClients.Add(client);
            }
        }
        
        // Remove disconnected clients
        foreach (var client in deadClients)
        {
            _sseClients.Remove(client);
            client.Dispose();
        }
    }
}