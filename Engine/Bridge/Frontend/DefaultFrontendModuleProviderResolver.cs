using System;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;
using Engine.Models;

namespace Engine.Bridge.Frontend;

internal sealed class DefaultFrontendModuleProviderResolver : IFrontendModuleProviderResolver
{
    private const string ProviderEnvironmentVariable = "WEBSTIR_FRONTEND_PROVIDER";
    private static readonly FrontendModuleProvider DefaultProvider = new("@webstir-io/webstir-frontend");

    public Task<FrontendModuleProvider> ResolveAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        string? overrideId = Environment.GetEnvironmentVariable(ProviderEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            return Task.FromResult(new FrontendModuleProvider(overrideId));
        }

        string? configuredProvider = ProviderConfigurationLoader.TryGetFrontendProvider(workspace);
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return Task.FromResult(new FrontendModuleProvider(configuredProvider));
        }

        return Task.FromResult(DefaultProvider);
    }
}
