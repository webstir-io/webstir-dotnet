using System.Threading.Tasks;

namespace Engine.Interfaces;

public interface IWorkflow
{
    string WorkflowName
    {
        get;
    }

    Task ExecuteAsync(string[] args);
}
