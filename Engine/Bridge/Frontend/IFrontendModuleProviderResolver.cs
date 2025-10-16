using System.Threading;
using System.Threading.Tasks;
using Engine.Models;

namespace Engine.Bridge.Frontend;

internal interface IFrontendModuleProviderResolver
{
    Task<FrontendModuleProvider> ResolveAsync(AppWorkspace workspace, CancellationToken cancellationToken);
}
