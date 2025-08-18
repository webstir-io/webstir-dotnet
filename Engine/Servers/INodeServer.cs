namespace Engine.Servers;

public interface INodeServer
{
    Task StartAsync(AppContext? context = null);
    Task StopAsync();
    Task RestartAsync();
    bool IsRunning { get; }
    event EventHandler<string>? OutputReceived;
}