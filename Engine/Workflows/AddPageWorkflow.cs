using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Adds a new page to the project in an isolated workspace
/// </summary>
public class AddPageWorkflow : BaseWorkflow
{
    public AddPageWorkflow(App app) 
        : base(app)
    {
    }

    public override string WorkflowName => "add-page";

    public override async Task ExecuteAsync(string[] args)
    {
        // Parse parameters from args
        var pageName = args.FirstOrDefault();
        if (string.IsNullOrEmpty(pageName))
        {
            LogError($"Usage: {App.Name} {App.Commands.AddPage} <page-name>");
            throw new ArgumentException($"Usage: {App.Name} {App.Commands.AddPage} <page-name>");
        }

        var workingDirectory = _app.WorkingDir;

        LogInfo($"Adding new page '{pageName}'...");

        // Initialize App to point to add-page workspace
        InitializeWorkspace();

        // Copy working directory to workspace
        if (workingDirectory.Exists)
        {
            CopyToWorkspace(workingDirectory);
        }

        // Validate page name
        if (string.IsNullOrWhiteSpace(pageName))
        {
            LogError("Page name cannot be empty");
            throw new ArgumentException("Page name is required");
        }

        // Check if page already exists
        var pagePath = _app.ClientPagesDir.CombinePath(pageName);
        if (Directory.Exists(pagePath))
        {
            LogError($"Page '{pageName}' already exists");
            throw new InvalidOperationException($"Page '{pageName}' already exists");
        }

        // Create page directory
        var pageDirectory = Directory.CreateDirectory(pagePath);
        LogInfo($"Created page directory: {pageDirectory.FullName}");

        // Auto-detect project mode and execute only relevant workers
        var projectMode = _app.DetectProjectMode();
        
        // Execute workers to add page-specific files
        await ExecuteWorkersAsync(async worker =>
        {
            LogInfo($"Adding page files with {worker.GetType().Name}...");
            await Task.Run(() => worker.AddPage(pageDirectory));
        }, projectMode);

        // Copy workspace back to working directory
        await CopyWorkspaceToTarget(workingDirectory);

        LogInfo($"Page '{pageName}' added successfully");
    }

    private async Task CopyWorkspaceToTarget(DirectoryInfo targetDir)
    {
        LogInfo($"Updating project files in {targetDir.FullName}...");
        
        await Task.Run(() =>
        {
            // Copy updated files back to target directory
            _app.WorkingDir.CopyTo(targetDir.FullName);
        });
    }
}