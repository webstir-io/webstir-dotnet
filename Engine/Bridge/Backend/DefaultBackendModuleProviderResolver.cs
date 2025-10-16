using System;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;
using Engine.Models;

namespace Engine.Bridge.Backend;

internal sealed class DefaultBackendModuleProviderResolver : IBackendModuleProviderResolver
{
    private const string ProviderEnvironmentVariable = "WEBSTIR_BACKEND_PROVIDER";
    private static readonly BackendModuleProvider DefaultProvider = new("@webstir-io/webstir-backend");

    public Task<BackendModuleProvider> ResolveAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        string? overrideId = Environment.GetEnvironmentVariable(ProviderEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            return Task.FromResult(new BackendModuleProvider(overrideId));
        }

        string? configuredProvider = ProviderConfigurationLoader.TryGetBackendProvider(workspace);
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return Task.FromResult(new BackendModuleProvider(configuredProvider));
        }

        return Task.FromResult(DefaultProvider);
    }
}
