namespace Engine;

public class AppSettings
{
    public int WebServerPort { get; set; } = 8088;
    public int ApiServerPort { get; set; } = 8008;
    // Security headers and precompression are enabled by default in the server
    // pipeline and are no longer configurable via settings.

    public string ApiServerUrl => $"http://localhost:{ApiServerPort}";
    public string WebServerUrl => $"http://localhost:{WebServerPort}";
}
