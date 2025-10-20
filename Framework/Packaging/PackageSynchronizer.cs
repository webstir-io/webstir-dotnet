using System;
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
                bool packageLockRemoved = false;

                if (NeedsInstall(frontendResult))
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-frontend");
                }

                if (NeedsInstall(testResult))
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-testing");
                }

                if (NeedsInstall(backendResult))
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    RemoveCachedPackage(workspace, logger, "@webstir-io/webstir-backend");
                }

                EnsureWorkspaceNpmrc(workspace, logger);
                logger?.LogInformation("[packages] Installing framework packages...");
                await workspace.RunNpmInstallAsync().ConfigureAwait(false);
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

                // Fallback: if packages still mismatch, force explicit install by spec
                if ((frontendResult?.VersionMismatch ?? false) ||
                    (testResult?.VersionMismatch ?? false) ||
                    (backendResult?.VersionMismatch ?? false))
                {
                    System.Collections.Generic.List<string> specs = new();
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
                        logger?.LogInformation("[packages] Retrying install with explicit specs: {Specs}", string.Join(", ", specs));
                        await workspace.InstallPackagesAsync(specs.ToArray());

                        // Re-evaluate after explicit install
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

    private static void EnsureWorkspaceNpmrc(IPackageWorkspace workspace, ILogger? logger)
    {
        // Opt-in only: by default we rely on user/repo npm config rather than
        // writing a per-workspace .npmrc beside the app. Set
        // WEBSTIR_WRITE_WORKSPACE_NPMRC=1 to enable this behavior.
        string? flag = Environment.GetEnvironmentVariable("WEBSTIR_WRITE_WORKSPACE_NPMRC");
        bool enabled = !string.IsNullOrWhiteSpace(flag) &&
            (flag.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             flag.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             flag.Equals("yes", StringComparison.OrdinalIgnoreCase));
        if (!enabled)
        {
            return;
        }

        try
        {
            string npmrcPath = Path.Combine(workspace.WorkingPath, ".npmrc");
            if (File.Exists(npmrcPath))
            {
                return; // respect existing project config
            }

            string content = "@webstir-io:registry=https://npm.pkg.github.com\n" +
                             "//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}\n" +
                             "always-auth=true\n";
            File.WriteAllText(npmrcPath, content);
            logger?.LogDebug("[packages] Wrote workspace .npmrc for GitHub Packages auth.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(ex, "Failed to write workspace .npmrc; relying on global npm config.");
        }
    }

    private static bool NeedsInstall<TEnsure>(TEnsure? result)
        where TEnsure : struct, IPackageEnsureResult =>
        result is { DependencyUpdated: true } or { VersionMismatch: true };

    private static void RemovePackageLockIfPresent(IPackageWorkspace workspace, ILogger? logger)
    {
        try
        {
            string packageLockPath = Path.Combine(workspace.WorkingPath, "package-lock.json");
            if (File.Exists(packageLockPath))
            {
                File.Delete(packageLockPath);
            }

            // npm may also honor a shrinkwrap file at the root
            string shrinkwrapPath = Path.Combine(workspace.WorkingPath, "npm-shrinkwrap.json");
            if (File.Exists(shrinkwrapPath))
            {
                File.Delete(shrinkwrapPath);
            }

            // npm v7+ can leave a sublock under node_modules which affects reify
            string subLockPath = Path.Combine(workspace.NodeModulesPath, ".package-lock.json");
            if (File.Exists(subLockPath))
            {
                File.Delete(subLockPath);
            }
        }
        catch (IOException ex)
        {
            logger?.LogDebug(ex, "Failed to remove package-lock.json while refreshing the packages.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogDebug(ex, "Insufficient permissions to remove package-lock.json while refreshing the packages.");
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

            // Also remove npm's alternative layouts for the same package
            // 1) version-suffixed directories under the scope (pkg@x.y.z)
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

                    // 2) hidden directories used during reify (.pkg-<random>)
                    foreach (string candidate in Directory.GetDirectories(scopePath, "." + name + "-*", SearchOption.TopDirectoryOnly))
                    {
                        TryDeleteDirectory(candidate, logger);
                    }
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing to remove.
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
