using System.Text;

namespace Engine.Pipelines.Core.Utilities;

public static class ContentSecurityPolicy
{
    public static string BuildDefaultPolicy()
    {
        StringBuilder csp = new();
        csp.Append("default-src 'self'; ");
        csp.Append("img-src 'self' data: https:; ");
        csp.Append("style-src 'self' 'unsafe-inline' https:; ");
        csp.Append("script-src 'self' https:; ");
        csp.Append("font-src 'self' data: https:; ");
        csp.Append("connect-src 'self' https:; ");
        csp.Append("object-src 'none'; ");
        csp.Append("base-uri 'self'; ");
        return csp.ToString();
    }
}

