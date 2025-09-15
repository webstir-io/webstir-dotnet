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
        await CopyErrorToDistAsync();
    }

    private async Task CopyErrorToDistAsync()
    {
        string sourceErrorJs = _workspace.FrontendBuildAppPath.Combine("error.js");
        if (!File.Exists(sourceErrorJs))
        {
            throw new FileNotFoundException($"Compiled error.js not found: {sourceErrorJs}");
        }

        string destApp = _workspace.FrontendDistAppPath;
        Directory.CreateDirectory(destApp);

        string content = await _builder.BundleSingleFileAsync(sourceErrorJs, EsbuildMode.Production, minify: true);
        string hash = ContentHashGenerator.ComputeHash(content);
        string hashedFileName = $"error.{hash}{FileExtensions.Js}";
        string hashedPath = Path.Combine(destApp, hashedFileName);
        await File.WriteAllTextAsync(hashedPath, content);
        await Precompression.CreatePrecompressedVariantsAsync(hashedPath);
    }

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = _workspace.FrontendPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            // {pageName} page entry
            // Import only what you need from '../../app/*' modules.
            """;

        File.WriteAllText(tsFilePath, tsContent);
        return Task.CompletedTask;
    }

}
