using System;
using Engine.Models;

namespace Engine.Bridge.Module;

internal static class ProviderSpecOverrides
{
    internal const string DefaultFrontendProviderId = "@webstir-io/webstir-frontend";
    internal const string DefaultBackendProviderId = "@webstir-io/webstir-backend";
    internal const string DefaultTestingProviderId = "@webstir-io/webstir-testing";

    private const string FrontendProviderEnvironmentVariable = "WEBSTIR_FRONTEND_PROVIDER";
    private const string BackendProviderEnvironmentVariable = "WEBSTIR_BACKEND_PROVIDER";
    private const string TestingProviderEnvironmentVariable = "WEBSTIR_TESTING_PROVIDER";

    private const string FrontendProviderSpecEnvironmentVariable = "WEBSTIR_FRONTEND_PROVIDER_SPEC";
    private const string BackendProviderSpecEnvironmentVariable = "WEBSTIR_BACKEND_PROVIDER_SPEC";
    private const string TestingProviderSpecEnvironmentVariable = "WEBSTIR_TESTING_PROVIDER_SPEC";

    internal static string ResolveFrontendProviderId(AppWorkspace workspace) =>
        ResolveProviderId(workspace, FrontendProviderEnvironmentVariable, ProviderConfigurationLoader.TryGetFrontendProvider, DefaultFrontendProviderId);

    internal static string ResolveBackendProviderId(AppWorkspace workspace) =>
        ResolveProviderId(workspace, BackendProviderEnvironmentVariable, ProviderConfigurationLoader.TryGetBackendProvider, DefaultBackendProviderId);

    internal static string ResolveTestingProviderId(AppWorkspace workspace) =>
        ResolveProviderId(workspace, TestingProviderEnvironmentVariable, ProviderConfigurationLoader.TryGetTestingProvider, DefaultTestingProviderId);

    internal static string? GetFrontendProviderSpec() => GetProviderSpec(FrontendProviderSpecEnvironmentVariable);

    internal static string? GetBackendProviderSpec() => GetProviderSpec(BackendProviderSpecEnvironmentVariable);

    internal static string? GetTestingProviderSpec() => GetProviderSpec(TestingProviderSpecEnvironmentVariable);

    internal static string? GetDefaultProviderSpec(string providerId, string defaultProviderId, string? providerSpec) =>
        string.Equals(providerId, defaultProviderId, StringComparison.Ordinal) ? providerSpec : null;

    private static string ResolveProviderId(
        AppWorkspace workspace,
        string environmentVariable,
        Func<AppWorkspace, string?> configurationResolver,
        string defaultProviderId)
    {
        string? overrideId = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            return overrideId;
        }

        string? configured = configurationResolver(workspace);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return defaultProviderId;
    }

    private static string? GetProviderSpec(string environmentVariable)
    {
        string? value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
