using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CLI;

public class Server()
{
    private const string _webRootPath = "build/bin";

    private readonly List<HttpContext> _sseClients = [];

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
        _sseClients.Clear();
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
}