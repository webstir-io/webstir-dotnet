namespace Engine.Models;

/// <summary>
/// Base class for workflow parameters
/// </summary>
public abstract class WorkflowParameters
{
    public DirectoryInfo WorkingDirectory { get; set; } = null!;
}

/// <summary>
/// Parameters for the Init workflow
/// </summary>
public class InitParameters : WorkflowParameters
{
    public ProjectMode Mode { get; set; } = ProjectMode.Fullstack;
}

/// <summary>
/// Parameters for the Build workflow
/// </summary>
public class BuildParameters : WorkflowParameters
{
    public bool ReleaseMode
    {
        get; set;
    }
    public bool CleanBuild
    {
        get; set;
    }
}

/// <summary>
/// Parameters for the Publish workflow
/// </summary>
public class PublishParameters : WorkflowParameters
{
    // Publish always uses release mode
    public bool CleanBuild { get; set; } = true;
}

/// <summary>
/// Parameters for the AddPage workflow
/// </summary>
public class AddPageParameters : WorkflowParameters
{
    public string PageName { get; set; } = null!;
}

/// <summary>
/// Parameters for the Watch workflow
/// </summary>
public class WatchParameters : WorkflowParameters
{
    public bool InitialBuild { get; set; } = true;
}
