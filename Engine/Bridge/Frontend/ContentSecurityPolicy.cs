using System.Text;

namespace Engine.Bridge.Frontend;

public static class ContentSecurityPolicy
{
    public static string BuildDefaultPolicy(bool isDevelopment = false)
    {
        StringBuilder csp = new();
        csp.Append("default-src 'self'; ");
        csp.Append("img-src 'self' data: https:; ");
        csp.Append("style-src 'self' 'unsafe-inline' https:; ");

        // Allow unsafe-inline for scripts in development for hot reload
        if (isDevelopment)
        {
            csp.Append("script-src 'self' 'unsafe-inline' https:; ");
        }
        else
        {
            csp.Append("script-src 'self' https:; ");
        }

        csp.Append("font-src 'self' data: https:; ");
        csp.Append("connect-src 'self' https:; ");
        csp.Append("object-src 'none'; ");
        csp.Append("base-uri 'self'; ");
        return csp.ToString();
    }
}
