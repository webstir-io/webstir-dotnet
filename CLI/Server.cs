using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;

namespace CLI;

public class Server()
{
    private const string _webRootPath = "build/bin";

    private WebSocket? _webSocket;

    public void Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions 
        { 
            WebRootPath = _webRootPath
        });

        builder.Services.AddDirectoryBrowser();

        var app = builder.Build();
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    _webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleWebSocketAsync(_webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
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
        var result = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        _ = Close(result);
        _webSocket?.Dispose();
    }

    private async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            Console.WriteLine("Handling websocket request");
        }
        
        await Close(result);
    }

    public async Task Update()
    {
        var responseMessage = $"Files changed";
        var responseBytes = Encoding.UTF8.GetBytes(responseMessage);

        if (_webSocket == null)
        {
            await Task.CompletedTask;
            return;
        }

        await _webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task Close(WebSocketReceiveResult result)
    {
        await _webSocket!.CloseAsync(result!.CloseStatus!.Value, result.CloseStatusDescription, CancellationToken.None);
    }
}