using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Adds a new page to the project in an isolated workspace
/// </summary>
public class AddPageWorkflow : BaseWorkflow<AddPageParameters>
{
    public AddPageWorkflow(App app) 
        : base(app)
    {
    }

    public override string WorkflowName => "add-page";

    public override async Task ExecuteAsync(AddPageParameters parameters)
    {
        LogInfo($"Adding new page '{parameters.PageName}'...");

        // Initialize App to point to add-page workspace
        InitializeWorkspace();

        // Copy working directory to workspace
        if (parameters.WorkingDirectory.Exists)
        {
            CopyToWorkspace(parameters.WorkingDirectory);
        }

        // Validate page name
        if (string.IsNullOrWhiteSpace(parameters.PageName))
        {
            LogError("Page name cannot be empty");
            throw new ArgumentException("Page name is required");
        }

        // Check if page already exists
        var pagePath = _app.ClientPagesDir.CombinePath(parameters.PageName);
        if (Directory.Exists(pagePath))
        {
            LogError($"Page '{parameters.PageName}' already exists");
            throw new InvalidOperationException($"Page '{parameters.PageName}' already exists");
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
        await CopyWorkspaceToTarget(parameters.WorkingDirectory);

        LogInfo($"Page '{parameters.PageName}' added successfully");
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