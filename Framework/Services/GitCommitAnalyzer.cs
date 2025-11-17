using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Utilities;
using Microsoft.Extensions.Logging;
using Utilities.Process;

namespace Framework.Services;

internal interface IGitCommitAnalyzer
{
    Task<CommitHistoryAnalysis> AnalyzeAsync(
        string repositoryRoot,
        PackageManifest manifest,
        string? sinceReference,
        CancellationToken cancellationToken);
}

internal sealed record CommitHistoryAnalysis(
    string PackageName,
    SemanticVersionBump? SuggestedBump,
    IReadOnlyList<string> CommitMessages);

internal sealed class GitCommitAnalyzer(IProcessRunner processRunner, ILogger<GitCommitAnalyzer> logger)
    : IGitCommitAnalyzer
{
    private const int MaxCommitsToInspect = 50;

    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<GitCommitAnalyzer> _logger = logger;

    public async Task<CommitHistoryAnalysis> AnalyzeAsync(
        string repositoryRoot,
        PackageManifest manifest,
        string? sinceReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        IReadOnlyList<string> commits = await GetCommitMessagesAsync(repositoryRoot, manifest, sinceReference, cancellationToken)
            .ConfigureAwait(false);

        SemanticVersionBump? suggestedBump = DetermineBump(commits);

        if (suggestedBump.HasValue)
        {
            _logger.LogInformation(
                "[packages] Conventional commits suggest a {Bump} bump for {Package}.",
                suggestedBump.Value,
                manifest.PackageName);
        }
        else if (commits.Count > 0)
        {
            _logger.LogInformation(
                "[packages] Conventional commits found for {Package} but none required a bump beyond patch.",
                manifest.PackageName);
        }

        return new CommitHistoryAnalysis(manifest.PackageName, suggestedBump, commits);
    }

    private async Task<IReadOnlyList<string>> GetCommitMessagesAsync(
        string repositoryRoot,
        PackageManifest manifest,
        string? sinceReference,
        CancellationToken cancellationToken)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, manifest.PackageDirectory)
            .Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            relativePath = ".";
        }

        string rangeArgument = string.IsNullOrWhiteSpace(sinceReference)
            ? $"--max-count={MaxCommitsToInspect}"
            : $"{QuoteRange(sinceReference!)}..HEAD --max-count={MaxCommitsToInspect}";

        string arguments = $"log {rangeArgument} --pretty=%s -- {QuotePath(relativePath)}";

        ProcessSpec spec = new()
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repositoryRoot
        };

        ProcessResult result = await _processRunner
            .RunAsync(spec, cancellationToken)
            .ConfigureAwait(false);

        if (!result.CompletedSuccessfully)
        {
            _logger.LogWarning(
                "[packages] Unable to inspect git history for {Package}. git {Arguments} returned {ExitCode}: {Error}",
                manifest.PackageName,
                arguments,
                result.ExitCode,
                string.IsNullOrWhiteSpace(result.StandardError) ? "(no stderr)" : result.StandardError);

            return Array.Empty<string>();
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return Array.Empty<string>();
        }

        string[] messages = result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return messages;
    }

    private static SemanticVersionBump? DetermineBump(IReadOnlyList<string> commits)
    {
        SemanticVersionBump? bump = null;

        foreach (string message in commits)
        {
            SemanticVersionBump candidate = Classify(message);
            bump = bump switch
            {
                null => candidate,
                _ => Max(bump.Value, candidate)
            };

            if (bump == SemanticVersionBump.Major)
            {
                break;
            }
        }

        if (bump == SemanticVersionBump.Patch && !commits.Any())
        {
            return null;
        }

        return bump;
    }

    private static SemanticVersionBump Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return SemanticVersionBump.Patch;
        }

        string subject = message.Trim();
        if (subject.Contains("BREAKING CHANGE", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticVersionBump.Major;
        }

        int colonIndex = subject.IndexOf(':', StringComparison.Ordinal);
        string prefix = colonIndex >= 0 ? subject[..colonIndex] : subject;

        if (prefix.Contains('!', StringComparison.Ordinal))
        {
            return SemanticVersionBump.Major;
        }

        int scopeIndex = prefix.IndexOf('(', StringComparison.Ordinal);
        string type = scopeIndex >= 0 ? prefix[..scopeIndex] : prefix;
        type = type.Trim();

        if (string.Equals(type, "feat", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticVersionBump.Minor;
        }

        if (type.Length == 0)
        {
            return SemanticVersionBump.Patch;
        }

        return SemanticVersionBump.Patch;
    }

    private static SemanticVersionBump Max(SemanticVersionBump left, SemanticVersionBump right)
    {
        return left switch
        {
            SemanticVersionBump.Major => SemanticVersionBump.Major,
            SemanticVersionBump.Minor when right == SemanticVersionBump.Major => SemanticVersionBump.Major,
            SemanticVersionBump.Minor => SemanticVersionBump.Minor,
            _ => right
        };
    }

    private static string QuotePath(string path)
    {
        if (path.Length == 0)
        {
            return "\"\"";
        }

        return path.Contains(' ', StringComparison.Ordinal)
            ? $"\"{path}\""
            : path;
    }

    private static string QuoteRange(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
    }
}
