using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Engine.Interfaces;

namespace Engine.Services;

public interface IWorkflowFactory
{
    Task ExecuteAsync(string commandName, string[] args);
}

public class WorkflowFactory(IEnumerable<IWorkflow> workflows) : IWorkflowFactory
{
    private readonly IEnumerable<IWorkflow> _workflows = workflows;

    public async Task ExecuteAsync(string commandName, string[] args)
    {
        IWorkflow workflow = _workflows.SingleOrDefault(w => w.WorkflowName == commandName)
            ?? throw new InvalidOperationException($"No workflow found for command '{commandName}'");

        await workflow.ExecuteAsync(args);
    }
}
