using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Logging;

namespace Framework.Commands;

internal sealed class PackagesVerifyCommand(PackageBuilder packageBuilder, IPackageMetadataService metadataService, ILogger<PackagesVerifyCommand> logger)
    : IPackagesSubcommand
{
    private readonly PackageBuilder _packageBuilder = packageBuilder;
    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly ILogger<PackagesVerifyCommand> _logger = logger;

    public string Name => "verify";

    public IReadOnlyCollection<string> Aliases { get; } = new[] { "check" };

    public async Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .ResolveAsync(context.RepositoryRoot, context.Selection, context.SinceReference, cancellationToken)
            .ConfigureAwait(false);

        if (manifests.Count == 0)
        {
            _logger.LogInformation(
                "[packages] No framework package changes detected; nothing to verify. Use '--all' to check all packages.");
            return 0;
        }

        bool includeFrontend = manifests.Any(manifest => manifest.Key.Equals("frontend", StringComparison.OrdinalIgnoreCase));
        bool includeTesting = manifests.Any(manifest => manifest.Key.Equals("testing", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("[packages] Verifying framework packages...");
        await _packageBuilder.VerifyAsync(context.RepositoryRoot, includeFrontend, includeTesting).ConfigureAwait(false);
        _logger.LogInformation("[packages] Verification succeeded.");
        return 0;
    }
}
