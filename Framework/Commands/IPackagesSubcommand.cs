using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Commands;

internal interface IPackagesSubcommand
{
    string Name
    {
        get;
    }

    IReadOnlyCollection<string> Aliases
    {
        get;
    }

    Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken);
}
