using System;
using System.Collections.Generic;
using System.IO;

namespace Framework.Packaging;

internal sealed record FrameworkPackageDescriptor(
    string Key,
    string PackageName,
    string PackageRelativePath,
    string TarballPrefix,
    string TarballPattern,
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
        "webstir-frontend-",
        "webstir-frontend-*.tgz",
        "WEBSTIR_FRONTEND_REGISTRY_SPEC",
        new[] { "node_modules" },
        "@webstir-io/webstir-frontend@{version}",
        "https://npm.pkg.github.com",
        "restricted",
        "GH_PACKAGES_TOKEN");

    internal static FrameworkPackageDescriptor Testing
    {
        get;
    } = new(
        "testing",
        "@webstir-io/webstir-test",
        Path.Combine("Framework", "Testing"),
        "webstir-test-",
        "webstir-test-*.tgz",
        "WEBSTIR_TEST_REGISTRY_SPEC",
        new[] { "node_modules", "dist" },
        "@webstir-io/webstir-test@{version}",
        "https://npm.pkg.github.com",
        "restricted",
        "GH_PACKAGES_TOKEN");

    internal static FrameworkPackageDescriptor Backend
    {
        get;
    } = new(
        "backend",
        "@webstir-io/webstir-backend",
        Path.Combine("Framework", "Backend"),
        "webstir-backend-",
        "webstir-backend-*.tgz",
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

    internal string? GetDefaultRegistrySpecifier(string version) =>
        string.IsNullOrWhiteSpace(DefaultRegistrySpecifierPattern)
            ? null
            : DefaultRegistrySpecifierPattern.Replace("{version}", version, StringComparison.Ordinal);

    internal string GetPackageSpec(string version) => $"{PackageName}@{version}";
}
