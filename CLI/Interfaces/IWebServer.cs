namespace CLI.Interfaces;

public interface IWebServer
{
    Task StartAsync();
    Task StopAsync();
    Task UpdateClientsAsync();
    bool IsRunning { get; }
}