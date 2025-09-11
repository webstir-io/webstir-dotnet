using System.Threading.Tasks;

namespace Engine.Workflows.Interfaces;

public interface IWorkflow
{
    string WorkflowName
    {
        get;
    }

    Task ExecuteAsync(string[] args);
}
