using System;
using System.Collections.Generic;
using Framework.Services;
using Framework.Utilities;

namespace Framework.Commands;

internal sealed record PackagesCommandContext(
    string Command,
    string RepositoryRoot,
    PackageSelection Selection,
    bool DryRun,
    SemanticVersionBump Bump,
    bool BumpExplicit,
    SemanticVersion? ExplicitVersion,
    bool PrintVersion,
    bool Interactive,
    string? SinceReference,
    IReadOnlyList<string> AdditionalArguments)
{
    public bool IsDryRun => DryRun;

    public bool HasExplicitVersion => ExplicitVersion is not null;

    public SemanticVersion EffectiveVersion(SemanticVersion current)
    {
        if (ExplicitVersion is not null)
        {
            return ExplicitVersion.Value;
        }

        return current.Increment(Bump);
    }

    public static PackagesCommandContext CreateDefault(string repositoryRoot, string command)
    {
        return new PackagesCommandContext(
            command,
            repositoryRoot,
            PackageSelection.ChangedPackages,
            DryRun: false,
            Bump: SemanticVersionBump.Patch,
            BumpExplicit: false,
            ExplicitVersion: null,
            PrintVersion: false,
            Interactive: false,
            SinceReference: null,
            AdditionalArguments: Array.Empty<string>());
    }
}
