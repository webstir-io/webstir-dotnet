using System.Threading.Tasks;
using Engine.Models;

namespace Engine.Interfaces;

public interface IWorkflowWorker
{
    int BuildOrder
    {
        get;
    }
    Task InitAsync(WorkspaceProfile profile);
    Task BuildAsync(string? changedFilePath = null);
    Task PublishAsync();
}
