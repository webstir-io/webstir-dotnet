using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Extensions;
using Framework.Packaging;
using Utilities.Process;

namespace Engine.Bridge;

public sealed class PackageManagerRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    public const string EnvironmentVariableName = "WEBSTIR_PACKAGE_MANAGER";

    private readonly string _workingPath;
    private readonly PackageManagerDescriptor _descriptor;

    private PackageManagerRunner(string workingPath, PackageManagerDescriptor descriptor)
    {
        _workingPath = workingPath ?? throw new ArgumentNullException(nameof(workingPath));
        _descriptor = descriptor;
    }

    public PackageManagerDescriptor Descriptor => _descriptor;

    public static PackageManagerRunner Create(string workingPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingPath);

        PackageManagerDescriptor descriptor = DetectDescriptor(workingPath);
        return new PackageManagerRunner(workingPath, descriptor);
    }

    public async Task InstallDependenciesAsync(CancellationToken cancellationToken = default)
    {
        switch (_descriptor.Kind)
        {
            case PackageManagerKind.Npm:
                await RunNpmInstallAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PackageManagerKind.Pnpm:
                await RunPnpmInstallAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PackageManagerKind.Yarn:
                await RunYarnInstallAsync(cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported package manager: {_descriptor.Kind}");
        }
    }

    public async Task InstallPackagesAsync(string[] packageSpecs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageSpecs);

        if (packageSpecs.Length == 0)
        {
            return;
        }

        if (_descriptor.Kind == PackageManagerKind.Npm)
        {
            string arguments = BuildNpmInstallArguments(packageSpecs);
            ProcessResult result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            if (!result.CompletedSuccessfully)
            {
                throw CreateInstallException(result, $"{_descriptor.DisplayName} install (explicit packages)");
            }
            return;
        }

        await InstallDependenciesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunNpmInstallAsync(CancellationToken cancellationToken)
    {
        string packageLockPath = _workingPath.Combine(Files.PackageLockJson);
        bool hasLock = packageLockPath.Exists();
        string arguments = hasLock ? "ci" : "install";

        ProcessResult result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (result.CompletedSuccessfully)
        {
            return;
        }

        if (hasLock)
        {
            DeleteIfExists(packageLockPath);
            result = await RunAsync("install", cancellationToken).ConfigureAwait(false);
            if (result.CompletedSuccessfully)
            {
                return;
            }
        }

        throw CreateInstallException(result, $"{_descriptor.DisplayName} {arguments}");
    }

    private async Task RunPnpmInstallAsync(CancellationToken cancellationToken)
    {
        string lockPath = Path.Combine(_workingPath, "pnpm-lock.yaml");
        bool hasLock = File.Exists(lockPath);
        string arguments = hasLock ? "install --frozen-lockfile" : "install";

        ProcessResult result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (result.CompletedSuccessfully)
        {
            return;
        }

        if (hasLock)
        {
            result = await RunAsync("install --no-frozen-lockfile", cancellationToken).ConfigureAwait(false);
            if (result.CompletedSuccessfully)
            {
                return;
            }
        }

        throw CreateInstallException(result, $"{_descriptor.DisplayName} {arguments}");
    }

    private async Task RunYarnInstallAsync(CancellationToken cancellationToken)
    {
        string lockPath = Path.Combine(_workingPath, "yarn.lock");
        bool hasLock = File.Exists(lockPath);

        string arguments = GetYarnInstallArguments(hasLock);
        ProcessResult result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (result.CompletedSuccessfully)
        {
            return;
        }

        if (hasLock)
        {
            result = await RunAsync("install", cancellationToken).ConfigureAwait(false);
            if (result.CompletedSuccessfully)
            {
                return;
            }
        }

        throw CreateInstallException(result, $"{_descriptor.DisplayName} {arguments}");
    }

    private async Task<ProcessResult> RunAsync(string arguments, CancellationToken cancellationToken)
    {
        ProcessRunner runner = new();
        (ProcessSpec spec, bool usedCorepack) = CreateSpec(arguments);
        try
        {
            return await runner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception) when (usedCorepack)
        {
            ProcessSpec fallback = BuildDirectSpec(arguments);
            return await runner.RunAsync(fallback, cancellationToken).ConfigureAwait(false);
        }
    }

    private static PackageManagerDescriptor DetectDescriptor(string workingPath)
    {
        if (TryParseManager(Environment.GetEnvironmentVariable(EnvironmentVariableName), out PackageManagerDescriptor overrideDescriptor))
        {
            return overrideDescriptor;
        }

        string packageJsonPath = Path.Combine(workingPath, "package.json");
        if (TryParseManagerFromPackageJson(packageJsonPath, out PackageManagerDescriptor manifestDescriptor))
        {
            return manifestDescriptor;
        }

        string pnpmLock = Path.Combine(workingPath, "pnpm-lock.yaml");
        if (File.Exists(pnpmLock))
        {
            return PackageManagerDescriptor.Create(PackageManagerKind.Pnpm, "pnpm", null);
        }

        string yarnLock = Path.Combine(workingPath, "yarn.lock");
        if (File.Exists(yarnLock))
        {
            return PackageManagerDescriptor.Create(PackageManagerKind.Yarn, "yarn", null);
        }

        string packageLock = Path.Combine(workingPath, "package-lock.json");
        if (File.Exists(packageLock))
        {
            return PackageManagerDescriptor.Create(PackageManagerKind.Npm, "npm", null);
        }

        return PackageManagerDescriptor.Create(PackageManagerKind.Npm, "npm", null);
    }

    private static bool TryParseManagerFromPackageJson(string packageJsonPath, out PackageManagerDescriptor descriptor)
    {
        descriptor = default;

        if (!File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            if (!document.RootElement.TryGetProperty("packageManager", out JsonElement element))
            {
                return false;
            }

            string? value = element.GetString();
            return TryParseManager(value, out descriptor);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryParseManager(string? value, out PackageManagerDescriptor descriptor)
    {
        descriptor = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        int separatorIndex = trimmed.IndexOf('@');
        string name = separatorIndex > 0 ? trimmed[..separatorIndex] : trimmed;
        string? version = separatorIndex > 0 ? trimmed[(separatorIndex + 1)..] : null;

        if (!TryMapPackageManager(name, out PackageManagerKind kind, out string executable))
        {
            return false;
        }

        descriptor = PackageManagerDescriptor.Create(kind, executable, version);
        return true;
    }

    private static bool TryMapPackageManager(string name, out PackageManagerKind kind, out string executable)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            kind = PackageManagerKind.Npm;
            executable = "npm";
            return false;
        }

        string normalized = name.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "npm":
                kind = PackageManagerKind.Npm;
                executable = "npm";
                return true;
            case "pnpm":
                kind = PackageManagerKind.Pnpm;
                executable = "pnpm";
                return true;
            case "yarn":
                kind = PackageManagerKind.Yarn;
                executable = "yarn";
                return true;
            default:
                kind = PackageManagerKind.Npm;
                executable = "npm";
                return false;
        }
    }

    private (ProcessSpec Spec, bool UsedCorepack) CreateSpec(string arguments)
    {
        if (TryBuildCorepackSpec(arguments, out ProcessSpec corepackSpec))
        {
            return (corepackSpec, true);
        }

        return (BuildDirectSpec(arguments), false);
    }

    private bool TryBuildCorepackSpec(string arguments, out ProcessSpec spec)
    {
        if (_descriptor.Kind is PackageManagerKind.Pnpm or PackageManagerKind.Yarn)
        {
            string managerSpec = string.IsNullOrWhiteSpace(_descriptor.Version)
                ? _descriptor.Executable
                : $"{_descriptor.Executable}@{_descriptor.Version}";

            spec = new ProcessSpec
            {
                FileName = "corepack",
                Arguments = $"{managerSpec} {arguments}".Trim(),
                WorkingDirectory = _workingPath,
                ExitTimeout = DefaultTimeout
            };
            return true;
        }

        spec = null!;
        return false;
    }

    private ProcessSpec BuildDirectSpec(string arguments) =>
        new()
        {
            FileName = _descriptor.Executable,
            Arguments = arguments,
            WorkingDirectory = _workingPath,
            ExitTimeout = DefaultTimeout
        };

    private string BuildNpmInstallArguments(string[] packageSpecs)
    {
        System.Text.StringBuilder builder = new();
        builder.Append("install --no-save");

        foreach (string spec in packageSpecs)
        {
            builder.Append(' ');
            builder.Append(spec);
        }

        return builder.ToString();
    }

    private string GetYarnInstallArguments(bool hasLock)
    {
        if (!hasLock)
        {
            return "install";
        }

        if (!string.IsNullOrWhiteSpace(_descriptor.Version) && int.TryParse(_descriptor.Version.Split('.')[0], out int major) && major >= 2)
        {
            return "install --immutable";
        }

        return "install --frozen-lockfile";
    }

    private static void DeleteIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // best effort
        }
        catch (UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private static Exception CreateInstallException(ProcessResult result, string description)
    {
        string errors = result.StandardError;
        string output = result.StandardOutput;
        string errorMessage = $"{description} failed (Exit Code: {result.ExitCode})";
        if (!string.IsNullOrWhiteSpace(errors))
        {
            errorMessage += $"\nErrors:\n{errors}";
        }
        if (!string.IsNullOrWhiteSpace(output))
        {
            errorMessage += $"\nOutput:\n{output}";
        }

        if (ContainsRegistryAuthHint(errors) || ContainsRegistryAuthHint(output))
        {
            errorMessage += "\nHint: Ensure @webstir-io resolves to https://registry.npmjs.org and npm auth is configured if required.";
        }

        return new Exception(errorMessage);
    }

    private static bool ContainsRegistryAuthHint(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Contains("E401", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E402", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E403", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E404", StringComparison.OrdinalIgnoreCase);
    }
}
