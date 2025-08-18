namespace Engine.Servers;

public interface IWebServer
{
    Task StartAsync(AppContext? context = null);
    Task StopAsync();
    Task UpdateClientsAsync();
    bool IsRunning { get; }
}