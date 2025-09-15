using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.JavaScript;

public class JsHandler(
    AppWorkspace workspace,
    JsBuilder builder,
    ILogger<JsHandler> logger) : IPageHandler
{
    private readonly AppWorkspace _workspace = workspace;
    private readonly JsBuilder _builder = builder;
    private readonly ILogger<JsHandler> _logger = logger;
    public int BuildOrder => 0;
    public int PublishOrder => 0;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        _builder.Build(diagnostics);
        await _builder.BundleAsync(EsbuildMode.Development, diagnostics);
        return;
    }

    public async Task PublishAsync()
    {
        DiagnosticCollection diagnostics = new();
        await _builder.BundleAsync(EsbuildMode.Production, diagnostics);
    }

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = _workspace.FrontendPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            // {pageName} page entry

            // Import shared app initialization (includes error handling)
            import '../../app/app';

            // Page-specific code here
            """;

        File.WriteAllText(tsFilePath, tsContent);
        return Task.CompletedTask;
    }

}
