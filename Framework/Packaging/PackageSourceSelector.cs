using System;

namespace Framework.Packaging;

public static class PackageSourceSelector
{
    private const string SourceEnvironmentVariable = "WEBSTIR_PACKAGE_SOURCE";
    private const string PreferEnvironmentVariable = "WEBSTIR_PREFER_REGISTRY";

    public static bool ShouldPreferRegistry()
    {
        string? source = Environment.GetEnvironmentVariable(SourceEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (source.Equals("registry", StringComparison.OrdinalIgnoreCase)
                || source.Equals("npm", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (source.Equals("archive", StringComparison.OrdinalIgnoreCase)
                || source.Equals("tarball", StringComparison.OrdinalIgnoreCase)
                || source.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        string? prefer = Environment.GetEnvironmentVariable(PreferEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(prefer))
        {
            return prefer.Equals("1", StringComparison.OrdinalIgnoreCase)
                || prefer.Equals("true", StringComparison.OrdinalIgnoreCase)
                || prefer.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
