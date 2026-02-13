using System;
using System.Collections.Generic;
using System.IO;

namespace Framework.Packaging;

internal sealed record FrameworkPackageDescriptor(
    string Key,
    string PackageName,
    string PackageRelativePath,
    string? WorkspaceSpecifierEnvironmentVariable,
    string? DefaultWorkspaceSpecifierPattern,
    string? RegistrySpecifierEnvironmentVariable,
    IReadOnlyCollection<string> CleanupDirectories,
    string? DefaultRegistrySpecifierPattern,
    string? PublishRegistryUrl,
    string PublishAccess,
    string? PublishAuthTokenEnvironmentVariable)
{
    internal static FrameworkPackageDescriptor Frontend
    {
        get;
    } = new(
        "frontend",
        "@webstir-io/webstir-frontend",
        Path.Combine("Framework", "Frontend"),
        "WEBSTIR_FRONTEND_WORKSPACE_SPEC",
        "^{version}",
        "WEBSTIR_FRONTEND_REGISTRY_SPEC",
        new[] { "node_modules" },
        "@webstir-io/webstir-frontend@{version}",
        "https://registry.npmjs.org",
        "public",
        "NPM_TOKEN");

    internal static FrameworkPackageDescriptor Testing
    {
        get;
    } = new(
        "testing",
        "@webstir-io/webstir-testing",
        Path.Combine("Framework", "Testing"),
        "WEBSTIR_TEST_WORKSPACE_SPEC",
        "^{version}",
        "WEBSTIR_TEST_REGISTRY_SPEC",
        new[] { "node_modules", "dist" },
        "@webstir-io/webstir-testing@{version}",
        "https://registry.npmjs.org",
        "public",
        "NPM_TOKEN");

    internal static FrameworkPackageDescriptor Backend
    {
        get;
    } = new(
        "backend",
        "@webstir-io/webstir-backend",
        Path.Combine("Framework", "Backend"),
        "WEBSTIR_BACKEND_WORKSPACE_SPEC",
        "^{version}",
        null,
        new[] { "node_modules", "dist" },
        "@webstir-io/webstir-backend@{version}",
        null,
        "restricted",
        null);

    internal static IReadOnlyList<FrameworkPackageDescriptor> All
    {
        get;
    } = new[] { Frontend, Testing, Backend };

    internal bool SupportsPublishing => !string.IsNullOrWhiteSpace(PublishRegistryUrl);

    internal string? GetWorkspaceSpecifierOverride() =>
        GetWorkspaceSpecifier(WorkspaceSpecifierEnvironmentVariable);

    internal string? GetDefaultWorkspaceSpecifier(string version) =>
        string.IsNullOrWhiteSpace(DefaultWorkspaceSpecifierPattern)
            ? null
            : DefaultWorkspaceSpecifierPattern.Replace("{version}", version, StringComparison.Ordinal);

    internal string? GetDefaultRegistrySpecifier(string version) =>
        string.IsNullOrWhiteSpace(DefaultRegistrySpecifierPattern)
            ? null
            : DefaultRegistrySpecifierPattern.Replace("{version}", version, StringComparison.Ordinal);

    internal string GetPackageSpec(string version) => $"{PackageName}@{version}";

    private static string? GetWorkspaceSpecifier(string? environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return null;
        }

        string? value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
