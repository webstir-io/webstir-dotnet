using System;

namespace Framework.Packaging;

internal static class RegistrySpecifierResolver
{
    public static string Resolve(FrameworkPackageMetadata metadata)
    {
        string? overrideValue = metadata.Name switch
        {
            "@webstir-io/webstir-frontend" => Environment.GetEnvironmentVariable("WEBSTIR_FRONTEND_REGISTRY_SPEC"),
            "@webstir-io/webstir-testing" => Environment.GetEnvironmentVariable("WEBSTIR_TEST_REGISTRY_SPEC"),
            "@webstir-io/webstir-backend" => Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_REGISTRY_SPEC"),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return overrideValue.Trim();
        }

        return metadata.RegistrySpecifier;
    }
}
