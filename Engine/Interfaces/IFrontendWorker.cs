using System.Threading.Tasks;
using Engine.Models;

namespace Engine.Interfaces;

public interface IFrontendWorker : IWorkflowWorker
{
    Task AddPageAsync(string pageName);
    Task StartWatchAsync();
    Task StopWatchAsync();
    FrontendHotUpdate? DequeueHotUpdate();
}
