using Engine.Models;

namespace Engine.Interfaces;

public interface IWorkflow
{
    string WorkflowName { get; }
    Task ExecuteAsync();
}

public interface IWorkflow<TParameters> : IWorkflow
{
    Task ExecuteAsync(TParameters parameters);
}