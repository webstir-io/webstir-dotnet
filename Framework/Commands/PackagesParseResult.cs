using System;
using System.Collections.Generic;
using Framework.Services;
using Framework.Utilities;

namespace Framework.Commands;

internal sealed record PackagesParseResult(
    string Command,
    bool ShowHelp,
    string RepositoryRoot,
    PackageSelection Selection,
    bool DryRun,
    SemanticVersionBump Bump,
    bool BumpExplicit,
    SemanticVersion? ExplicitVersion,
    bool PrintVersion,
    bool Interactive,
    string? SinceReference,
    IReadOnlyList<string> AdditionalArguments);
