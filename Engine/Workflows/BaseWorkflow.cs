using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Models;
using Engine.Interfaces;

namespace Engine.Workflows;

public abstract class BaseWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : IWorkflow
{
    protected readonly AppWorkspace Context = context;
    protected IEnumerable<IWorkflowWorker> Workers { get; } = workers;
    protected WorkspaceProfile WorkspaceProfile
    {
        get; private set;
    }
    protected void SetWorkspaceProfile(WorkspaceProfile profile) => WorkspaceProfile = profile;
    protected IFrontendWorker Frontend => Workers.OfType<IFrontendWorker>().Single();
    public abstract string WorkflowName
    {
        get;
    }

    public virtual async Task ExecuteAsync(string[] args)
    {
        InitializeWorkspace(args);
        await ExecuteWorkflowAsync(args);
    }

    protected abstract Task ExecuteWorkflowAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IWorkflowWorker, Task> workerAction, WorkspaceProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(workerAction);

        WorkspaceProfile effectiveProfile = profile ?? WorkspaceProfile;
        IEnumerable<IWorkflowWorker> workers = GetFilteredWorkers(effectiveProfile);

        IEnumerable<IGrouping<int, IWorkflowWorker>> workerGroups = workers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (IGrouping<int, IWorkflowWorker> group in workerGroups)
        {
            List<IWorkflowWorker> workersInGroup = [.. group];
            foreach (IWorkflowWorker worker in workersInGroup)
            {
                await workerAction(worker);
            }
        }
    }

    private IEnumerable<IWorkflowWorker> GetFilteredWorkers(WorkspaceProfile profile)
    {
        if (profile.HasFrontend && profile.HasBackend)
        {
            return Workers;
        }

        if (profile.HasFrontend)
        {
            return Workers.Where(w => w is IFrontendWorker or Engine.Bridge.Shared.SharedWorker);
        }

        if (profile.HasBackend)
        {
            return Workers.Where(w => w is not IFrontendWorker);
        }

        return Enumerable.Empty<IWorkflowWorker>();
    }

    protected async Task ExecuteBuildAsync() => await ExecuteWorkersAsync(async worker => await worker.BuildAsync(), WorkspaceProfile);

    protected async Task ExecuteBuildAsync(WorkspaceProfile profile) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(), profile);

    protected async Task ExecuteBuildAsync(string? changedFilePath) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(changedFilePath), WorkspaceProfile);

    protected async Task ExecuteBuildAsync(string? changedFilePath, WorkspaceProfile profile) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(changedFilePath), profile);

    protected virtual void InitializeWorkspace(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        string? workspaceOverride = ResolveWorkspaceOverride(filteredArgs);
        if (!string.IsNullOrWhiteSpace(workspaceOverride))
        {
            string fullPath = Path.GetFullPath(workspaceOverride, Context.WorkingPath);
            if (Directory.Exists(fullPath))
            {
                Context.Initialize(fullPath);
                filteredArgs = filteredArgs.Where(arg => arg != workspaceOverride).ToArray();
            }
        }

        string? projectName = GetProjectFromFlags(filteredArgs);

        if (!string.IsNullOrEmpty(projectName))
        {
            string projectPath = Context.WorkingPath.Combine(projectName);
            if (!projectPath.Exists())
                throw new WorkflowUsageException($"Project directory '{projectName}' not found in current directory.");

            Context.Initialize(projectPath);
            WorkspaceProfile = Context.DetectWorkspaceProfile();
            return;
        }

        if (Context.WorkingPath.Combine(Folders.Src).Exists())
        {
            WorkspaceProfile = Context.DetectWorkspaceProfile();
            return;
        }

        List<string> validProjects = [.. Context.WorkingPath.Folders()
            .Where(projectPath => projectPath.Combine(Folders.Src).Exists())];

        if (validProjects.Count == 0)
            throw new WorkflowUsageException(
                "No webstir project found here. Run 'webstir init <name>' first or pass the target path (e.g., 'webstir build ../my-app').");

        if (validProjects.Count == 1)
        {
            Context.Initialize(validProjects.Single());
            WorkspaceProfile = Context.DetectWorkspaceProfile();
            return;
        }

        IEnumerable<string?> projectNames = validProjects.Select(Path.GetFileName);
        throw new WorkflowUsageException(
            $"Multiple projects found: {string.Join(", ", projectNames)}. " +
            $"Specify which project to use: {WorkflowName} <project-name> or {WorkflowName} {ProjectOptions.ProjectName} <project-name>.");
    }

    private static string? ResolveWorkspaceOverride(string[] args)
    {
        for (int index = args.Length - 1; index >= 0; index--)
        {
            string candidate = args[index];
            if (candidate.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            if (!LooksLikeWorkspacePath(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool LooksLikeWorkspacePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Path.IsPathRooted(value) ||
            value.StartsWith(".", StringComparison.Ordinal) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar);
    }

    protected static string? GetProjectFromFlags(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int index = 0; index < args.Length; index++)
        {
            if ((args[index] == ProjectOptions.ProjectName || args[index] == ProjectOptions.ProjectNameShort) && index + 1 < args.Length)
                return args[index + 1];
        }

        return null;
    }
}
