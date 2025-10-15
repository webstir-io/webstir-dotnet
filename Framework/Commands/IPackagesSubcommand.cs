namespace Framework.Commands;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
