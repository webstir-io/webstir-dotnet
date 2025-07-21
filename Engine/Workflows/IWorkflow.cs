namespace Engine.Servers;

public interface IWorkflow
{
    string WorkflowName { get; }
    Task ExecuteAsync(string[] args);
}