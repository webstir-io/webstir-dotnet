namespace CLI.Interfaces;

public interface INodeServer
{
    Task StartAsync();
    Task StopAsync();
    Task RestartAsync();
    bool IsRunning { get; }
    event EventHandler<string>? OutputReceived;
}