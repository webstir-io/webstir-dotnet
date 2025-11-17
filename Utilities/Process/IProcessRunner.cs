using System.Threading;
using System.Threading.Tasks;

namespace Utilities.Process;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);

    Task<IProcessHandle> StartAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}
