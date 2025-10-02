using System;
using System.IO;
using Engine;

namespace Tests.Framework;

public static class WorkspaceManager
{
    private static readonly object SeedLock = new();
    private static readonly object SeedNodeModulesLock = new();
    private static bool _seedBaselineReady;

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

            ProcessRunner.ProcessResult init = context.Cli.Run(
                $"{Commands.Init} {ProjectOptions.ProjectName} {Folders.Seed}",
                CacheRoot,
                timeoutMs: 20000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} (seed baseline) failed. Error: {init.Error}");

            ProcessRunner.ProcessResult install = context.Cli.Run(
                $"{Commands.Install} {ProjectOptions.ProjectName} {Folders.Seed} {InstallOptions.Clean}",
                CacheRoot,
                timeoutMs: 60000);
            Assert.AreEqual(0, install.ExitCode, $"{Commands.Install} (seed baseline) failed. Error: {install.Error}");

            string nodeModulesRoot = Path.Combine(SeedBaselinePath, "node_modules");
            if (!Directory.Exists(nodeModulesRoot) || Directory.GetFileSystemEntries(nodeModulesRoot).Length == 0)
            {
                ProcessRunner.ProcessResult npmInstall = ProcessRunner.Run(new ProcessRunOptions
                {
                    FileName = "npm",
                    Arguments = "install",
                    WorkingDirectory = SeedBaselinePath,
                    ExitTimeoutMs = 60000
                });
                Assert.AreEqual(0, npmInstall.ExitCode, $"npm install (seed baseline) failed. Error: {npmInstall.Error}");
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

            Assert.IsTrue(Directory.Exists(SeedBaselinePath), "Seed baseline directory missing before dependency restore.");

            ProcessRunner.ProcessResult restore = ProcessRunner.Run(new ProcessRunOptions
            {
                FileName = "npm",
                Arguments = "install",
                WorkingDirectory = SeedBaselinePath,
                ExitTimeoutMs = 90000
            });

            Assert.AreEqual(0, restore.ExitCode, $"npm install (seed baseline) failed. Error: {restore.Error}");
            Assert.IsTrue(
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
}
