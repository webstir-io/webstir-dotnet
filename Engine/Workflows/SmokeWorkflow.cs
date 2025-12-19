using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Engine.Bridge.Backend;
using Engine.Bridge.Module;
using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.Workflows;

public sealed class SmokeWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    ILogger<SmokeWorkflow> logger) : BaseWorkflow(context, workers)
{
    private readonly ILogger<SmokeWorkflow> _logger = logger;

    public override string WorkflowName => Commands.Smoke;

    public override async Task ExecuteAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string repositoryRoot = Context.WorkingPath;
        string workspacePath = await PrepareWorkspaceAsync(args, repositoryRoot);

        string backendPackagePath = Path.GetFullPath(Path.Combine(repositoryRoot, "Framework", "Backend"));
        string backendProviderEntry = Path.Combine(backendPackagePath, "dist", "index.js");

        string? originalWorkspaceSpec = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_WORKSPACE_SPEC");
        string? originalProviderOverride = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_PROVIDER");

        Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_WORKSPACE_SPEC", $"file:{backendPackagePath}");
        Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_PROVIDER", backendProviderEntry);

        try
        {
            Context.Initialize(workspacePath);
            SetWorkspaceProfile(Context.DetectWorkspaceProfile());
            await ExecuteWorkflowAsync(args).ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_WORKSPACE_SPEC", originalWorkspaceSpec);
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_PROVIDER", originalProviderOverride);
        }
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync().ConfigureAwait(false);

        ModuleBuildManifest manifest = await BackendManifestLoader.LoadAsync(Context).ConfigureAwait(false);
        ReportManifest(manifest);
    }

    private Task<string> PrepareWorkspaceAsync(string[] args, string repositoryRoot)
    {
        string? overridePath = ResolveWorkspaceOverride(args);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string fullPath = Path.GetFullPath(overridePath, repositoryRoot);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Smoke workspace not found at '{fullPath}'.");
            }

            _logger.LogInformation("Using existing workspace at {Workspace}", fullPath);
            return Task.FromResult(fullPath);
        }

        throw new InvalidOperationException("Smoke workflow requires an explicit workspace path. Pass the workspace directory as an argument.");
    }

    private static string? ResolveWorkspaceOverride(string[] args)
    {
        for (int index = 1; index < args.Length; index++)
        {
            string candidate = args[index];
            if (candidate.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private async Task ConfigureWorkspacePackageAsync(string workspacePath, string repositoryRoot)
    {
        string packageJsonPath = Path.Combine(workspacePath, Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return;
        }

        string packageJson = await File.ReadAllTextAsync(packageJsonPath);

        JsonNode? rootNode = JsonNode.Parse(packageJson);
        if (rootNode is not JsonObject root)
        {
            throw new InvalidOperationException("Unable to parse workspace package.json for smoke run.");
        }

        if (root["dependencies"] is not JsonObject dependencies)
        {
            dependencies = [];
            root["dependencies"] = dependencies;
        }

        string contractPath = Path.GetFullPath(Path.Combine(repositoryRoot, "Framework", "Contracts", "module-contract"));
        string backendPath = Path.GetFullPath(Path.Combine(repositoryRoot, "Framework", "Backend"));

        string contractSpecifier = $"file:{Path.GetRelativePath(workspacePath, contractPath).Replace(Path.DirectorySeparatorChar, '/')}";
        string backendSpecifier = $"file:{Path.GetRelativePath(workspacePath, backendPath).Replace(Path.DirectorySeparatorChar, '/')}";

        dependencies["@webstir-io/module-contract"] = contractSpecifier;
        dependencies["@webstir-io/webstir-backend"] = backendSpecifier;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(options));

        string lockFilePath = Path.Combine(workspacePath, Files.PackageLockJson);
        if (File.Exists(lockFilePath))
        {
            File.Delete(lockFilePath);
        }
    }

    private static async Task EnsureBackendTsconfigAsync(string workspacePath)
    {
        string backendTsconfigPath = Path.Combine(workspacePath, "src", "backend", "tsconfig.json");
        if (File.Exists(backendTsconfigPath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(backendTsconfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const string contents = """
{
  "extends": "../../tsconfig.json",
  "compilerOptions": {
    "outDir": "../../build/backend",
    "rootDir": ".",
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "incremental": true,
    "tsBuildInfoFile": "../../build/backend/.tsbuildinfo"
  },
  "include": ["**/*.ts"],
  "exclude": ["node_modules"]
}
""";

        await File.WriteAllTextAsync(backendTsconfigPath, contents);
    }

    private static async Task InstallDependenciesAsync(string workspacePath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "npm",
            Arguments = "install --no-audit --no-fund --include=optional",
            WorkingDirectory = workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new()
        {
            StartInfo = startInfo
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Console.WriteLine(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Console.Error.WriteLine(eventArgs.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Dependency install failed with exit code {process.ExitCode}.");
        }
    }

    private void ReportManifest(ModuleBuildManifest manifest)
    {
        Console.WriteLine($"Backend manifest written to {Context.BackendManifestPath}");

        if (manifest.Module is not { } module)
        {
            _logger.LogError("Backend manifest did not include module metadata.");
            Environment.ExitCode = 1;
            return;
        }

        int routeCount = module.Routes?.Count ?? 0;
        int capabilityCount = module.Capabilities?.Count ?? 0;

        _logger.LogInformation(
            "Module {Name}@{Version} reported {RouteCount} route(s) and {CapabilityCount} capability flag(s).",
            module.Name,
            module.Version,
            routeCount,
            capabilityCount);

        if (routeCount == 0)
        {
            _logger.LogWarning("Smoke check: manifest contains no route definitions.");
        }

        if (module.Routes is { Count: > 0 } routes)
        {
            Console.WriteLine();
            Console.WriteLine("Routes:");
            foreach (RouteDefinition route in routes)
            {
                Console.WriteLine($"  {route.Method} {route.Path} ({route.Name})");
            }
        }
    }
}
