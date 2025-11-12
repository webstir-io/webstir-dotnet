using System;

namespace Engine.Workflows;

public sealed class WorkflowUsageException : Exception
{
    public WorkflowUsageException(string message)
        : base(message)
    {
    }
}
