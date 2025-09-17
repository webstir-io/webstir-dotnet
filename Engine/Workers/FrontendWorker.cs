using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Engine.Interfaces;

namespace Engine.Workers;

public partial class FrontendWorker(
    AppWorkspace workspace,
    IEnumerable<IFrontendHandler> frontendHandlers,
    ILogger<FrontendWorker> logger) : IFrontendWorker
{
    private readonly ILogger<FrontendWorker> _logger = logger;
    private readonly IEnumerable<IFrontendHandler> _frontendHandlers = frontendHandlers;
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.FrontendPath, workspace.FrontendPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Frontend))
            return;

        PackageEnsureResult ensureResult = await TestPackageInstaller.EnsureAsync(workspace);

        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
        {
            NpmHelper.RunNpmInstall(workspace.WorkingPath);
            ensureResult = await TestPackageInstaller.EnsureAsync(workspace);
        }

        if (ensureResult.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(ensureResult.InstalledVersion)
                ? "missing"
                : ensureResult.InstalledVersion;
            _logger.LogWarning(
                "@webstir/test {InstalledVersion} detected but {ExpectedVersion} is bundled. Run npm install to refresh dependencies.",
                installed,
                ensureResult.Metadata.Version);
        }

        await RunHandlersInOrderAsync(
            h => h.BuildOrder,
            h => h.BuildAsync(changedFilePath),
            nameof(BuildAsync));
    }

    public async Task PublishAsync()
    {
        if (workspace.FrontendDistPath.Exists())
        {
            TryClearDirectory(workspace.FrontendDistPath);
        }
        else
        {
            workspace.FrontendDistPath.Create();
        }

        PackageEnsureResult ensureResult = await TestPackageInstaller.EnsureAsync(workspace);

        if (ensureResult.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(ensureResult.InstalledVersion)
                ? "missing"
                : ensureResult.InstalledVersion;
            _logger.LogWarning(
                "@webstir/test {InstalledVersion} detected but {ExpectedVersion} is bundled. Run npm install to refresh dependencies.",
                installed,
                ensureResult.Metadata.Version);
        }

        await RunHandlersInOrderAsync(
            h => h.PublishOrder,
            h => h.PublishAsync(),
            nameof(PublishAsync));
    }

    private async Task RunHandlersInOrderAsync(
        Func<IFrontendHandler, int> orderSelector,
        Func<IFrontendHandler, Task<bool>> handlerAction,
        string operationName)
    {
        IOrderedEnumerable<IGrouping<int, IFrontendHandler>> handlersByOrder = _frontendHandlers
            .GroupBy(orderSelector)
            .OrderBy(g => g.Key);

        foreach (IGrouping<int, IFrontendHandler> group in handlersByOrder)
        {
            List<Task<bool>> tasks = [];
            foreach (IFrontendHandler handler in group)
            {
                tasks.Add(handlerAction(handler));
            }

            bool[] results = await Task.WhenAll(tasks);
            bool groupSuccess = results.All(r => r);

            if (!groupSuccess)
            {
                _logger.LogError("{Operation} failed at order {Order} with errors", operationName, group.Key);
                break;
            }
        }
    }

    public async Task AddPageAsync(string pageName)
    {
        List<Task<bool>> tasks = [];
        foreach (IPageHandler handler in _frontendHandlers.OfType<IPageHandler>())
        {
            tasks.Add(handler.AddPageAsync(pageName));
        }
        bool[] results = await Task.WhenAll(tasks);

        if (!results.All(r => r))
        {
            _logger.LogError("Failed to add page {PageName}", pageName);
        }
    }

    private void TryClearDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            path.Create();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to clear directory {Path}: {Message}", path, ex.Message);
        }
    }
}
