using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;
using Engine.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Services;

/// <summary>
/// Convention-based workflow factory that automatically routes commands to workflows
/// </summary>
public interface IWorkflowFactory
{
    Task ExecuteAsync(string commandName, string[] args);
}

public class WorkflowFactory : IWorkflowFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly App _app;

    public WorkflowFactory(IServiceProvider serviceProvider, App app)
    {
        _serviceProvider = serviceProvider;
        _app = app;
    }

    public async Task ExecuteAsync(string commandName, string[] args)
    {
        switch (commandName)
        {
            case App.Commands.Init:
                await ExecuteWorkflow<InitWorkflow, InitParameters>(args, ParseInitParameters);
                break;

            case App.Commands.Build:
                await ExecuteWorkflow<BuildWorkflow, BuildParameters>(args, ParseBuildParameters);
                break;

            case App.Commands.Publish:
                await ExecuteWorkflow<PublishWorkflow, PublishParameters>(args, ParsePublishParameters);
                break;

            case App.Commands.AddPage:
                await ExecuteWorkflow<AddPageWorkflow, AddPageParameters>(args, ParseAddPageParameters);
                break;

            default:
                throw new InvalidOperationException($"No workflow found for command '{commandName}'");
        }
    }

    private async Task ExecuteWorkflow<TWorkflow, TParameters>(
        string[] args, 
        Func<string[], TParameters> parameterParser)
        where TWorkflow : class, IWorkflow<TParameters>
        where TParameters : WorkflowParameters
    {
        var workflow = _serviceProvider.GetRequiredService<TWorkflow>();
        var parameters = parameterParser(args);
        await workflow.ExecuteAsync(parameters);
    }

    private InitParameters ParseInitParameters(string[] args)
    {
        return new InitParameters
        {
            WorkingDirectory = _app.WorkingDir,
            Mode = ParseProjectMode(args)
        };
    }

    private BuildParameters ParseBuildParameters(string[] args)
    {
        var explicitClean = args.Contains(App.Options.Clean);
        var needsClean = explicitClean || ShouldCleanBuild();
        
        return new BuildParameters
        {
            WorkingDirectory = _app.WorkingDir,
            ReleaseMode = false,
            CleanBuild = needsClean
        };
    }

    private bool ShouldCleanBuild()
    {
        var buildWorkspaceDir = App.OutDir.CreateSubDirectory("build");
        var buildDir = buildWorkspaceDir.CreateSubDirectory(App.Folders.Build);
        
        if (!buildDir.Exists)
            return true;

        // Check for TypeScript build info files
        const string tsBuildInfoFile = ".tsbuildinfo";
        var clientBuildDir = buildDir.CreateSubDirectory(App.Folders.Client);
        var serverBuildDir = buildDir.CreateSubDirectory(App.Folders.Server);
        
        var clientTsConfig = clientBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();
        var serverTsConfig = serverBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();

        var clientSrcExists = _app.WorkingDir.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Client}").Exists;
        var serverSrcExists = _app.WorkingDir.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Server}").Exists;

        if (clientSrcExists && clientTsConfig == null)
            return true;
        if (serverSrcExists && serverTsConfig == null)
            return true;

        return false;
    }

    private PublishParameters ParsePublishParameters(string[] args)
    {
        return new PublishParameters
        {
            WorkingDirectory = _app.WorkingDir,
            CleanBuild = args.Contains(App.Options.Clean)
        };
    }

    private AddPageParameters ParseAddPageParameters(string[] args)
    {
        var pageName = args.FirstOrDefault();
        if (string.IsNullOrEmpty(pageName))
        {
            throw new ArgumentException($"Usage: {App.Name} {App.Commands.AddPage} <page-name>");
        }

        return new AddPageParameters
        {
            WorkingDirectory = _app.WorkingDir,
            PageName = pageName
        };
    }

    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(App.Options.ClientOnly)) return ProjectMode.ClientOnly;
        if (args.Contains(App.Options.ServerOnly)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}