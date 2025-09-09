using System.Threading.Tasks;

namespace Engine.Workflows.Interfaces;

public interface IFrontendWorker : IWorkflowWorker
{
    Task AddPageAsync(string pageName);
}

