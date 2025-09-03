namespace Engine;

public class AppSettings
{
    public int WebServerPort { get; set; } = 8088;
    public int ApiServerPort { get; set; } = 8008;

    public string ApiServerUrl => $"http://localhost:{ApiServerPort}";
    public string WebServerUrl => $"http://localhost:{WebServerPort}";
}
