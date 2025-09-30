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
        Func<bool, Task<FrontendPackageEnsureResult>>? ensureFrontend,
        Func<bool, Task<PackageEnsureResult>>? ensureTesting,
        bool includeFrontend = true,
        bool includeTesting = true,
        bool autoInstall = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        bool preferRegistry = PackageSourceSelector.ShouldPreferRegistry();
        if (preferRegistry)
        {
            logger?.LogInformation("[packages] Prefer registry packages (WEBSTIR_PACKAGE_SOURCE=registry).");
        }

        FrontendPackageEnsureResult? frontendResult = includeFrontend && ensureFrontend is not null
            ? await ensureFrontend(preferRegistry)
            : null;

        PackageEnsureResult? testResult = includeTesting && ensureTesting is not null
            ? await ensureTesting(preferRegistry)
            : null;

        bool needsInstall = NeedsInstall(frontendResult) || NeedsInstall(testResult);
        bool installPerformed = false;
        bool installRequiredButSkipped = false;

        if (needsInstall)
        {
            if (autoInstall)
            {
                bool packageLockRemoved = false;

                if (frontendResult?.TarballUpdated == true)
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    logger?.LogInformation("[packages] Clearing cached frontend package before install.");
                    RemoveCachedPackage(workspace, logger, "@electric-coding-llc/webstir-frontend");
                }

                if (testResult?.TarballUpdated == true)
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    logger?.LogInformation("[packages] Clearing cached testing package before install.");
                    RemoveCachedPackage(workspace, logger, "@electric-coding-llc/webstir-test");
                }

                logger?.LogInformation("[packages] Installing framework packages...");
                await workspace.RunNpmInstallAsync();
                installPerformed = true;

                if (includeFrontend && ensureFrontend is not null)
                {
                    frontendResult = await ensureFrontend(preferRegistry);
                }

                if (includeTesting && ensureTesting is not null)
                {
                    testResult = await ensureTesting(preferRegistry);
                }
            }
            else
            {
                installRequiredButSkipped = true;
            }
        }

        return new PackageEnsureSummary(frontendResult, testResult, installPerformed, installRequiredButSkipped);
    }

    private static bool NeedsInstall<TEnsure>(TEnsure? result)
        where TEnsure : struct, IPackageEnsureResult =>
        result is
        {
            ToolsAdded: true
        } or
        {
            DependencyUpdated: true
        } or
        {
            TarballUpdated: true
        } or
        {
            VersionMismatch: true
        };

    private static void RemovePackageLockIfPresent(IPackageWorkspace workspace, ILogger? logger)
    {
        try
        {
            string packageLockPath = Path.Combine(workspace.WorkingPath, "package-lock.json");
            if (File.Exists(packageLockPath))
            {
                File.Delete(packageLockPath);
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
}

public readonly record struct PackageEnsureSummary(
    FrontendPackageEnsureResult? Frontend,
    PackageEnsureResult? Testing,
    bool InstallPerformed,
    bool InstallRequiredButSkipped)
{
    public bool HasVersionMismatch =>
        (Frontend?.VersionMismatch ?? false) ||
        (Testing?.VersionMismatch ?? false);
}
