namespace Engine;

public class AppSettings
{
    public int WebServerPort { get; set; } = 8088;
    public int ApiServerPort { get; set; } = 8008;
    public bool EnableSecurityHeaders { get; set; } = false;
    public bool EnablePrecompression { get; set; } = false;
    public bool EnableEarlyHints { get; set; } = false;
    public string? SourceMapToken
    {
        get; set;
    }

    public string ApiServerUrl => $"http://localhost:{ApiServerPort}";
    public string WebServerUrl => $"http://localhost:{WebServerPort}";
}
