using System.Threading.Tasks;

namespace Engine.Interfaces;

public interface IFrontendWorker : IWorkflowWorker
{
    Task AddPageAsync(string pageName);
    Task StartWatchAsync();
    Task StopWatchAsync();
}
