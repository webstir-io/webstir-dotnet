using Engine.Servers;

namespace Engine.Services;

/// <summary>
/// Convention-based workflow factory that automatically routes commands to workflows
/// </summary>
public interface IWorkflowFactory
{
    Task ExecuteAsync(string commandName, string[] args);
}

public class WorkflowFactory(IEnumerable<IWorkflow> workflows) : IWorkflowFactory
{
    private readonly IEnumerable<IWorkflow> _workflows = workflows;

    public async Task ExecuteAsync(string commandName, string[] args)
    {
        var workflow = _workflows.FirstOrDefault(w => w.WorkflowName == commandName)
            ?? throw new InvalidOperationException($"No workflow found for command '{commandName}'");

        await workflow.ExecuteAsync(args);
    }
}