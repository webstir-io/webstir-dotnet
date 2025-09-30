namespace Framework;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class FrameworkCommandRouter
{
    private readonly ILogger<FrameworkCommandRouter> _logger;

    public FrameworkCommandRouter(ILogger<FrameworkCommandRouter> logger)
    {
        _logger = logger;
    }

    public Task<int> ExecuteAsync(string[] args)
    {
        _logger.LogInformation("framework console scaffolding ready. Arguments: {Arguments}", args);
        return Task.FromResult(0);
    }
}
