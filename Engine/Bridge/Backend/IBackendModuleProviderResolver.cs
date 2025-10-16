using System.Threading;
using System.Threading.Tasks;
using Engine.Models;

namespace Engine.Bridge.Backend;

internal interface IBackendModuleProviderResolver
{
    Task<BackendModuleProvider> ResolveAsync(AppWorkspace workspace, CancellationToken cancellationToken);
}
