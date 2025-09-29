using System;
using System.IO;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Bridge.Frontend;
using Engine.Bridge.Test;
using Engine.Extensions;
using Microsoft.Extensions.Logging;

namespace Engine.Bridge.Packaging;

internal static class ToolchainSynchronizer
{
    internal static async Task<ToolchainEnsureSummary> EnsureAsync(
        AppWorkspace workspace,
        ILogger? logger,
        bool includeFrontend = true,
        bool includeTesting = true,
        bool autoInstall = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        bool preferRegistry = PackageSourceSelector.ShouldPreferRegistry();
        if (preferRegistry)
        {
            logger?.LogInformation("[toolchain] Prefer registry packages (WEBSTIR_PACKAGE_SOURCE=registry).");
        }

        FrontendPackageEnsureResult? frontendResult = includeFrontend
            ? await FrontendPackageInstaller.EnsureAsync(workspace, preferRegistry)
            : null;

        PackageEnsureResult? testResult = includeTesting
            ? await TestPackageInstaller.EnsureAsync(workspace, preferRegistry)
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

                    logger?.LogInformation("[toolchain] Clearing cached frontend package before install.");
                    RemoveCachedPackage(workspace, logger, "@electric-coding-llc/webstir-frontend");
                }

                if (testResult?.TarballUpdated == true)
                {
                    if (!packageLockRemoved)
                    {
                        RemovePackageLockIfPresent(workspace, logger);
                        packageLockRemoved = true;
                    }

                    logger?.LogInformation("[toolchain] Clearing cached testing package before install.");
                    RemoveCachedPackage(workspace, logger, "@electric-coding-llc/webstir-test");
                }

                logger?.LogInformation("[toolchain] Installing framework packages...");
                NpmHelper.RunNpmInstall(workspace.WorkingPath);
                installPerformed = true;

                if (includeFrontend)
                {
                    frontendResult = await FrontendPackageInstaller.EnsureAsync(workspace, preferRegistry);
                }

                if (includeTesting)
                {
                    testResult = await TestPackageInstaller.EnsureAsync(workspace, preferRegistry);
                }
            }
            else
            {
                installRequiredButSkipped = true;
            }
        }

        return new ToolchainEnsureSummary(frontendResult, testResult, installPerformed, installRequiredButSkipped);
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

    private static void RemovePackageLockIfPresent(AppWorkspace workspace, ILogger? logger)
    {
        try
        {
            string packageLockPath = Path.Combine(workspace.WorkingPath, Files.PackageLockJson);
            if (File.Exists(packageLockPath))
            {
                File.Delete(packageLockPath);
            }
        }
        catch (IOException ex)
        {
            logger?.LogDebug(ex, "Failed to remove package-lock.json while refreshing the toolchain.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogDebug(ex, "Insufficient permissions to remove package-lock.json while refreshing the toolchain.");
        }
    }

    private static void RemoveCachedPackage(AppWorkspace workspace, ILogger? logger, string packageName)
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
            logger?.LogDebug(ex, "Failed to remove cached package {Package} while refreshing the toolchain.", packageName);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogDebug(ex, "Insufficient permissions to remove cached package {Package} while refreshing the toolchain.", packageName);
        }
    }
}

internal readonly record struct ToolchainEnsureSummary(
    FrontendPackageEnsureResult? Frontend,
    PackageEnsureResult? Testing,
    bool InstallPerformed,
    bool InstallRequiredButSkipped)
{
    public bool HasVersionMismatch =>
        (Frontend?.VersionMismatch ?? false) ||
        (Testing?.VersionMismatch ?? false);
}
