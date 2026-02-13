namespace Engine;

public class AppSettings
{
    public string WebServerHost { get; set; } = "localhost";
    public int WebServerPort { get; set; } = 8088;
    public int ApiServerPort { get; set; } = 8008;
    // Security headers and precompression are enabled by default in the server
    // pipeline and are no longer configurable via settings.

    public string ApiServerUrl => $"http://localhost:{ApiServerPort}";
    public string WebServerUrl => $"http://{NormalizeHost(WebServerHost)}:{WebServerPort}";
    public string WebServerListenUrl => $"http://{WebServerHost}:{WebServerPort}";

    private static string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "localhost";
        }

        return host switch
        {
            "0.0.0.0" => "localhost",
            "::" => "localhost",
            "*" => "localhost",
            _ => host
        };
    }
}
