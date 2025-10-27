using System;
using System.IO;
using System.Threading.Tasks;
using Engine;
using Engine.Bridge;
using Xunit;

namespace Tester.Infrastructure;

public static class WorkspaceManager
{
    private static readonly object SeedLock = new();
    private static readonly object SeedNodeModulesLock = new();
    private static readonly object RegistryCheckLock = new();
    private static bool _seedBaselineReady;

    public static bool EnsureLocalPackagesReady()
    {
        // Registry overrides are disabled; rely on user-level npm config.
        ClearRegistryOverrides();
        // Always return true; authentication is handled via user ~/.npmrc or env tokens.
        return true;
    }

    private static string CacheRoot => Path.Combine(Paths.OutPath, ".baselines");
    private static string SeedBaselinePath => Path.Combine(CacheRoot, Folders.Seed);

    public static string CreateSeedWorkspace(TestCaseContext context, string workspaceName)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureSeedBaseline(context);

        string destination = Path.Combine(Paths.OutPath, workspaceName);
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        CopyWorkspaceFromBaseline(destination);
        return destination;
    }

    public static void EnsureSeedWorkspaceReady(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureSeedBaseline(context);
        CopyWorkspaceFromBaseline(Path.Combine(Paths.OutPath, Folders.Seed));
    }

    private static void EnsureSeedBaseline(TestCaseContext context)
    {
        ClearRegistryOverrides();

        if (_seedBaselineReady && Directory.Exists(SeedBaselinePath))
        {
            return;
        }

        lock (SeedLock)
        {
            if (_seedBaselineReady && Directory.Exists(SeedBaselinePath))
            {
                return;
            }

            Directory.CreateDirectory(CacheRoot);
            if (Directory.Exists(SeedBaselinePath))
            {
                Directory.Delete(SeedBaselinePath, recursive: true);
            }

            ProcessRunner.ProcessResult init = context.Run(
                $"{Commands.Init} {ProjectOptions.ProjectName} {Folders.Seed}",
                CacheRoot,
                timeoutMs: 20000);
            Assert.Equal(0, init.ExitCode);

            // Seed workspace now exists; write auth if available before install
            EnsureWorkspaceNpmAuth(SeedBaselinePath);

            ProcessRunner.ProcessResult install = context.Run(
                $"{Commands.Install} {ProjectOptions.ProjectName} {Folders.Seed} {InstallOptions.Clean}",
                CacheRoot,
                timeoutMs: 60000);
            if (install.ExitCode != 0)
            {
                Console.WriteLine($"[seed] webstir install failed (exit {install.ExitCode})");
                if (!string.IsNullOrEmpty(install.Output))
                {
                    Console.WriteLine(install.Output);
                }
                if (!string.IsNullOrEmpty(install.Error))
                {
                    Console.WriteLine(install.Error);
                }
            }
            Assert.Equal(0, install.ExitCode);

            string nodeModulesRoot = Path.Combine(SeedBaselinePath, "node_modules");
            if (!Directory.Exists(nodeModulesRoot) || Directory.GetFileSystemEntries(nodeModulesRoot).Length == 0)
            {
                PackageManagerRunner runner = PackageManagerRunner.Create(SeedBaselinePath);
                Task installTask = runner.InstallDependenciesAsync();
                installTask.GetAwaiter().GetResult();
            }

            CopyWorkspaceFromBaseline(Path.Combine(Paths.OutPath, Folders.Seed));
            _seedBaselineReady = true;
        }
    }

    private static void CopyWorkspaceFromBaseline(string destination)
    {
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        CopyDirectory(SeedBaselinePath, destination, skipNodeModules: true);
        EnsureNodeModules(destination);
    }

    private static void CopyDirectory(string source, string destination, bool skipNodeModules)
    {
        string? nodeModulesRoot = skipNodeModules ? Path.Combine(source, "node_modules") : null;

        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (nodeModulesRoot is not null && directory.StartsWith(nodeModulesRoot, StringComparison.Ordinal))
            {
                continue;
            }

            string targetDir = destination + directory[source.Length..];
            Directory.CreateDirectory(targetDir);
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (nodeModulesRoot is not null && file.StartsWith(nodeModulesRoot, StringComparison.Ordinal))
            {
                continue;
            }

            string targetFile = destination + file[source.Length..];
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            FileAttributes attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                FileInfo linkInfo = new(file);
                string? linkTarget = linkInfo.LinkTarget;
                if (!string.IsNullOrEmpty(linkTarget))
                {
                    File.CreateSymbolicLink(targetFile, linkTarget);
                    continue;
                }
            }

            File.Copy(file, targetFile, overwrite: true);
            File.SetAttributes(targetFile, attributes);
        }
    }

    private static void EnsureNodeModules(string destination)
    {
        EnsureBaselineNodeModules();

        string sourceNodeModules = Path.Combine(SeedBaselinePath, "node_modules");
        string destinationNodeModules = Path.Combine(destination, "node_modules");

        if (Directory.Exists(destinationNodeModules))
        {
            Directory.Delete(destinationNodeModules, recursive: true);
        }
        else if (File.Exists(destinationNodeModules))
        {
            File.Delete(destinationNodeModules);
        }

        if (TryCreateSymbolicLink(destinationNodeModules, sourceNodeModules))
        {
            return;
        }

        CopyDirectory(sourceNodeModules, destinationNodeModules, skipNodeModules: false);
    }

    private static void EnsureBaselineNodeModules()
    {
        lock (SeedNodeModulesLock)
        {
            string sourceNodeModules = Path.Combine(SeedBaselinePath, "node_modules");
            if (Directory.Exists(sourceNodeModules) && Directory.GetFileSystemEntries(sourceNodeModules).Length > 0)
            {
                return;
            }

            Assert.True(Directory.Exists(SeedBaselinePath), "Seed baseline directory missing before dependency restore.");

            ProcessRunner.ProcessResult restore = ProcessRunner.Run(new ProcessRunOptions
            {
                FileName = "npm",
                Arguments = "install",
                WorkingDirectory = SeedBaselinePath,
                ExitTimeoutMs = 90000
            });

            Assert.Equal(0, restore.ExitCode);
            Assert.True(
                Directory.Exists(sourceNodeModules) && Directory.GetFileSystemEntries(sourceNodeModules).Length > 0,
                "Seed baseline node_modules missing after dependency restore.");
        }
    }

    private static bool TryCreateSymbolicLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void ClearRegistryOverrides()
    {
        Environment.SetEnvironmentVariable("WEBSTIR_FRONTEND_REGISTRY_SPEC", null);
        Environment.SetEnvironmentVariable("WEBSTIR_TEST_REGISTRY_SPEC", null);
        Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_REGISTRY_SPEC", null);
    }

    // No-op: authentication is expected to come from user-level ~/.npmrc or env tokens.
    private static bool EnsureRegistryCredentials() => true;

    private static bool IsRegistryAuthFailure(ProcessRunner.ProcessResult result)
    {
        string output = $"{result.Output}{Environment.NewLine}{result.Error}".ToLowerInvariant();
        return output.Contains("npm.pkg.github.com") ||
            output.Contains("e401") ||
            output.Contains("authentication token not provided") ||
            output.Contains("unable to authenticate");
    }

    private static void EnsureWorkspaceNpmAuth(string workspacePath)
    {
        try
        {
            // If a workspace .npmrc already exists, leave it alone.
            string npmrcPath = Path.Combine(workspacePath, ".npmrc");
            if (File.Exists(npmrcPath))
            {
                return;
            }

            // Try environment tokens first.
            string? token = Environment.GetEnvironmentVariable("GH_PACKAGES_TOKEN");
            token ??= Environment.GetEnvironmentVariable("NODE_AUTH_TOKEN");

            // If missing, attempt to extract from user ~/.npmrc
            if (string.IsNullOrWhiteSpace(token))
            {
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string userNpmrc = Path.Combine(userHome, ".npmrc");
                if (File.Exists(userNpmrc))
                {
                    foreach (string line in File.ReadAllLines(userNpmrc))
                    {
                        const string key = "//npm.pkg.github.com/:_authToken=";
                        if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        {
                            token = line[key.Length..].Trim();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return; // fall back to user config only
            }

            string content = "@webstir-io:registry=https://npm.pkg.github.com\n" +
                             $"//npm.pkg.github.com/:_authToken={token}\n" +
                             "always-auth=true\n";
            File.WriteAllText(npmrcPath, content);
        }
        catch
        {
            // Best-effort only; rely on user-level config if writing fails.
        }
    }
}
