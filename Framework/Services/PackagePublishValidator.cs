using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Microsoft.Extensions.Logging;
using Utilities.Process;

namespace Framework.Services;

internal interface IPackagePublishValidator
{
    Task ValidateAsync(string repositoryRoot, PackageSelection selection, string? sinceReference, CancellationToken cancellationToken);
}

internal sealed class PackagePublishValidator(
    IPackageMetadataService metadataService,
    IProcessRunner processRunner,
    ILogger<PackagePublishValidator> logger) : IPackagePublishValidator
{
    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<PackagePublishValidator> _logger = logger;

    public async Task ValidateAsync(string repositoryRoot, PackageSelection selection, string? sinceReference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .ResolveAsync(repositoryRoot, selection, sinceReference, cancellationToken)
            .ConfigureAwait(false);

        List<FrameworkPackageDescriptor> descriptors = new();
        foreach (PackageManifest manifest in manifests)
        {
            FrameworkPackageDescriptor? descriptor = FrameworkPackageDescriptor.All
                .FirstOrDefault(candidate => string.Equals(candidate.Key, manifest.Key, StringComparison.OrdinalIgnoreCase));
            if (descriptor is null || !descriptor.SupportsPublishing)
            {
                continue;
            }

            descriptors.Add(descriptor);
        }

        if (descriptors.Count == 0)
        {
            _logger.LogDebug("[packages] No publishable framework packages detected; skipping registry validation.");
            return;
        }

        List<string> errors = new();

        string? configError = EnsureNpmConfiguration(repositoryRoot);
        if (!string.IsNullOrWhiteSpace(configError))
        {
            _logger.LogError("[packages] {Message}", configError);
            errors.Add(configError);
        }

        IReadOnlyList<string> authErrors = EnsureAuthentication(descriptors);
        foreach (string error in authErrors)
        {
            _logger.LogError("[packages] {Message}", error);
            errors.Add(error);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        await ValidateRegistriesAsync(repositoryRoot, descriptors, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("[packages] Registry validation succeeded.");
    }

    private string? EnsureNpmConfiguration(string repositoryRoot)
    {
        string defaultConfigPath = Path.Combine(repositoryRoot, ".npmrc");
        string? userConfig = Environment.GetEnvironmentVariable("NPM_CONFIG_USERCONFIG");

        if (!string.IsNullOrWhiteSpace(userConfig))
        {
            if (!File.Exists(userConfig))
            {
                if (File.Exists(defaultConfigPath))
                {
                    _logger.LogWarning(
                        "[packages] NPM_CONFIG_USERCONFIG points to '{Config}' but it does not exist; using {Default} instead.",
                        userConfig,
                        defaultConfigPath);
                    Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", defaultConfigPath);
                }
                else
                {
                    return $"NPM_CONFIG_USERCONFIG is set to '{userConfig}' but the file does not exist. Provide a valid npm config or unset the variable.";
                }
            }

            return null;
        }

        if (File.Exists(defaultConfigPath))
        {
            _logger.LogInformation("[packages] Using npm config from {Path}.", defaultConfigPath);
            Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", defaultConfigPath);
        }

        return null;
    }

    private IReadOnlyList<string> EnsureAuthentication(IEnumerable<FrameworkPackageDescriptor> descriptors)
    {
        HashSet<string> requiredTokens = new(StringComparer.OrdinalIgnoreCase);
        List<string> errors = new();

        foreach (FrameworkPackageDescriptor descriptor in descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.PublishAuthTokenEnvironmentVariable))
            {
                continue;
            }

            if (!requiredTokens.Add(descriptor.PublishAuthTokenEnvironmentVariable))
            {
                continue;
            }

            string? token = Environment.GetEnvironmentVariable(descriptor.PublishAuthTokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(token))
            {
                string registry = descriptor.PublishRegistryUrl ?? "the configured registry";
                errors.Add(
                    $"{descriptor.PublishAuthTokenEnvironmentVariable} is required to publish packages to {registry}. Set the token or rerun with --dry-run.");
            }
        }

        return errors;
    }

    private async Task ValidateRegistriesAsync(
        string repositoryRoot,
        IEnumerable<FrameworkPackageDescriptor> descriptors,
        CancellationToken cancellationToken)
    {
        HashSet<string> registries = new(StringComparer.OrdinalIgnoreCase);

        foreach (FrameworkPackageDescriptor descriptor in descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.PublishRegistryUrl))
            {
                continue;
            }

            if (!registries.Add(descriptor.PublishRegistryUrl))
            {
                continue;
            }

            _logger.LogInformation("[packages] Verifying access to {Registry}...", descriptor.PublishRegistryUrl);

            ProcessSpec spec = new()
            {
                FileName = "npm",
                Arguments = $"ping --registry \"{descriptor.PublishRegistryUrl}\"",
                WorkingDirectory = repositoryRoot
            };

            ProcessResult result = await _processRunner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            if (result.CompletedSuccessfully)
            {
                continue;
            }

            string details = string.Join(
                " ",
                new[] { result.StandardError, result.StandardOutput }.Where(content => !string.IsNullOrWhiteSpace(content))).Trim();

            if (!string.IsNullOrEmpty(details))
            {
                details = $" Details: {details}";
            }

            throw new InvalidOperationException($"npm ping failed for registry '{descriptor.PublishRegistryUrl}'.{details}");
        }
    }
}
