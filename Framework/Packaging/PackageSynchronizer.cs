using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Framework.Packaging;

public static class PackageSynchronizer
{
    public static async Task<PackageEnsureSummary> EnsureAsync(
        IPackageWorkspace workspace,
        ILogger? logger,
        Func<Task<FrontendPackageEnsureResult>>? ensureFrontend,
        Func<Task<PackageEnsureResult>>? ensureTesting,
        Func<Task<PackageEnsureResult>>? ensureBackend = null,
        bool includeFrontend = true,
        bool includeTesting = true,
        bool includeBackend = false,
        bool autoInstall = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        PackageManagerDescriptor manager = workspace.PackageManager;

        FrontendPackageEnsureResult? frontendResult = includeFrontend && ensureFrontend is not null
            ? await ensureFrontend().ConfigureAwait(false)
            : null;

        PackageEnsureResult? testResult = includeTesting && ensureTesting is not null
            ? await ensureTesting().ConfigureAwait(false)
            : null;

        PackageEnsureResult? backendResult = includeBackend && ensureBackend is not null
            ? await ensureBackend().ConfigureAwait(false)
            : null;

        bool needsInstall = NeedsInstall(frontendResult) || NeedsInstall(testResult) || NeedsInstall(backendResult);
        bool installPerformed = false;
        bool installRequiredButSkipped = false;

        if (needsInstall)
        {
            if (autoInstall)
            {
                bool lockFilesRemoved = false;

                if (NeedsInstall(frontendResult))
                {
                    if (!lockFilesRemoved)
                    {
                        RemoveLockFiles(workspace, manager, logger);
                        lockFilesRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-frontend");
                }

                if (NeedsInstall(testResult))
                {
                    if (!lockFilesRemoved)
                    {
                        RemoveLockFiles(workspace, manager, logger);
                        lockFilesRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-testing");
                }

                if (NeedsInstall(backendResult))
                {
                    if (!lockFilesRemoved)
                    {
                        RemoveLockFiles(workspace, manager, logger);
                        lockFilesRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-backend");
                }

                EnsureWorkspaceRegistryConfig(workspace, logger);
                logger?.LogInformation(
                    "[packages] Installing framework packages with {Manager}...",
                    manager.Executable);
                await workspace.InstallDependenciesAsync().ConfigureAwait(false);
                installPerformed = true;

                if (includeFrontend && ensureFrontend is not null)
                {
                    frontendResult = await ensureFrontend().ConfigureAwait(false);
                }

                if (includeTesting && ensureTesting is not null)
                {
                    testResult = await ensureTesting().ConfigureAwait(false);
                }

                if (includeBackend && ensureBackend is not null)
                {
                    backendResult = await ensureBackend().ConfigureAwait(false);
                }

                if ((frontendResult?.VersionMismatch ?? false) ||
                    (testResult?.VersionMismatch ?? false) ||
                    (backendResult?.VersionMismatch ?? false))
                {
                    List<string> specs = new();
                    if (frontendResult is { VersionMismatch: true } f)
                    {
                        specs.Add(RegistrySpecifierResolver.Resolve(f.Metadata));
                    }
                    if (testResult is { VersionMismatch: true } t)
                    {
                        specs.Add(RegistrySpecifierResolver.Resolve(t.Metadata));
                    }
                    if (backendResult is { VersionMismatch: true } b)
                    {
                        specs.Add(RegistrySpecifierResolver.Resolve(b.Metadata));
                    }

                    if (specs.Count > 0)
                    {
                        logger?.LogInformation(
                            "[packages] Retrying install with explicit specs using {Manager}: {Specs}",
                            manager.Executable,
                            string.Join(", ", specs));
                        await workspace.InstallPackagesAsync(specs.ToArray()).ConfigureAwait(false);

                        if (includeFrontend && ensureFrontend is not null)
                        {
                            frontendResult = await ensureFrontend().ConfigureAwait(false);
                        }
                        if (includeTesting && ensureTesting is not null)
                        {
                            testResult = await ensureTesting().ConfigureAwait(false);
                        }
                        if (includeBackend && ensureBackend is not null)
                        {
                            backendResult = await ensureBackend().ConfigureAwait(false);
                        }
                    }
                }
            }
            else
            {
                installRequiredButSkipped = true;
            }
        }

        return new PackageEnsureSummary(frontendResult, testResult, backendResult, installPerformed, installRequiredButSkipped);
    }

    private static void EnsureWorkspaceRegistryConfig(IPackageWorkspace workspace, ILogger? logger)
    {
        string? flag = Environment.GetEnvironmentVariable("WEBSTIR_WRITE_WORKSPACE_NPMRC");
        bool disabled = !string.IsNullOrWhiteSpace(flag) &&
            (flag.Equals("0", StringComparison.OrdinalIgnoreCase) ||
             flag.Equals("false", StringComparison.OrdinalIgnoreCase) ||
             flag.Equals("no", StringComparison.OrdinalIgnoreCase));
        if (disabled)
        {
            return;
        }

        try
        {
            string npmrcPath = Path.Combine(workspace.WorkingPath, ".npmrc");
            if (File.Exists(npmrcPath))
            {
                return;
            }

            string content = "@webstir-io:registry=https://registry.npmjs.org\n";
            File.WriteAllText(npmrcPath, content);
            logger?.LogDebug("[packages] Wrote workspace .npmrc for npmjs package resolution.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(ex, "Failed to write workspace .npmrc; relying on global npm config.");
        }
    }

    private static bool NeedsInstall<TEnsure>(TEnsure? result)
        where TEnsure : struct, IPackageEnsureResult =>
        result is { DependencyUpdated: true } or { VersionMismatch: true };

    private static void RemoveLockFiles(IPackageWorkspace workspace, PackageManagerDescriptor manager, ILogger? logger)
    {
        try
        {
            foreach (string path in EnumerateLockFiles(workspace, manager))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (IOException ex)
        {
            logger?.LogDebug(ex, "Failed to remove lock files while refreshing packages for {Manager}.", manager.Executable);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogDebug(ex, "Insufficient permissions to remove lock files while refreshing packages for {Manager}.", manager.Executable);
        }
    }

    private static IEnumerable<string> EnumerateLockFiles(IPackageWorkspace workspace, PackageManagerDescriptor manager)
    {
        string root = workspace.WorkingPath;
        switch (manager.Kind)
        {
            case PackageManagerKind.Npm:
                yield return Path.Combine(root, "package-lock.json");
                yield return Path.Combine(root, "npm-shrinkwrap.json");
                yield return Path.Combine(workspace.NodeModulesPath, ".package-lock.json");
                break;
            case PackageManagerKind.Pnpm:
                yield return Path.Combine(root, "pnpm-lock.yaml");
                break;
            case PackageManagerKind.Yarn:
                yield return Path.Combine(root, "yarn.lock");
                yield return Path.Combine(root, ".pnp.cjs");
                yield return Path.Combine(root, ".pnp.loader.mjs");
                break;
            default:
                yield break;
        }
    }

    private static void RemoveCachedPackage(IPackageWorkspace workspace, ILogger? logger, string packageName)
    {
        try
        {
            string packagePath = Path.Combine(workspace.NodeModulesPath, packageName);
            if (Directory.Exists(packagePath))
            {
                Directory.Delete(packagePath, recursive: true);
            }

            string scope = packageName.Contains('/') ? packageName.Split('/')[0] : string.Empty;
            string name = packageName.Contains('/') ? packageName.Split('/')[1] : packageName;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                string scopePath = Path.Combine(workspace.NodeModulesPath, scope);
                if (Directory.Exists(scopePath))
                {
                    foreach (string candidate in Directory.GetDirectories(scopePath, name + "@*", SearchOption.TopDirectoryOnly))
                    {
                        TryDeleteDirectory(candidate, logger);
                    }

                    foreach (string candidate in Directory.GetDirectories(scopePath, "." + name + "-*", SearchOption.TopDirectoryOnly))
                    {
                        TryDeleteDirectory(candidate, logger);
                    }
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException ex)
        {
            logger?.LogDebug(ex, "Failed to remove cached package {Package} while refreshing the packages.", packageName);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogDebug(ex, "Insufficient permissions to remove cached package {Package} while refreshing the packages.", packageName);
        }
    }

    private static void TryDeleteDirectory(string path, ILogger? logger)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(ex, "Failed to remove directory while refreshing the packages: {Path}", path);
        }
    }
}

public readonly record struct PackageEnsureSummary(
    FrontendPackageEnsureResult? Frontend,
    PackageEnsureResult? Testing,
    PackageEnsureResult? Backend,
    bool InstallPerformed,
    bool InstallRequiredButSkipped)
{
    public bool HasVersionMismatch =>
        (Frontend?.VersionMismatch ?? false) ||
        (Testing?.VersionMismatch ?? false) ||
        (Backend?.VersionMismatch ?? false);
}
