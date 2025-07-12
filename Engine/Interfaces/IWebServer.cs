namespace Engine.Interfaces;

public interface IWebServer
{
    Task StartAsync();
    Task StopAsync();
    Task UpdateClientsAsync();
    bool IsRunning { get; }
}