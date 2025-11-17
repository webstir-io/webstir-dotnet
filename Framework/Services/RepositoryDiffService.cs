using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utilities.Process;

namespace Framework.Services;

internal interface IRepositoryDiffService
{
    Task<RepositoryDiffResult> GetStatusAsync(string repositoryRoot, RepositoryDiffOptions options, CancellationToken cancellationToken);
}

internal sealed record RepositoryDiffOptions(string? SinceRef = null, bool IncludeUntracked = true);

internal sealed record RepositoryDiffResult(IReadOnlyCollection<string> Paths)
{
    public bool HasChanges => Paths.Count > 0;
}

internal sealed class RepositoryDiffService(IProcessRunner processRunner, ILogger<RepositoryDiffService> logger) : IRepositoryDiffService
{
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<RepositoryDiffService> _logger = logger;

    public async Task<RepositoryDiffResult> GetStatusAsync(string repositoryRoot, RepositoryDiffOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"Repository root '{repositoryRoot}' not found.");
        }

        string arguments = BuildArguments(options);
        ProcessSpec spec = new()
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repositoryRoot
        };

        ProcessResult result = await _processRunner.RunAsync(
            spec,
            cancellationToken).ConfigureAwait(false);

        if (!result.CompletedSuccessfully)
        {
            _logger.LogWarning("git {Arguments} exited with code {ExitCode}. stderr: {StdErr}", arguments, result.ExitCode, result.StandardError);
            throw new InvalidOperationException($"Failed to query git status. Command 'git {arguments}' exited with {result.ExitCode}.");
        }

        IReadOnlyCollection<string> paths = ParsePaths(result.StandardOutput);
        return new RepositoryDiffResult(paths);
    }

    private static string BuildArguments(RepositoryDiffOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SinceRef))
        {
            string since = Quote(options.SinceRef!);
            return $"diff --name-only {since}";
        }

        string untracked = options.IncludeUntracked ? "all" : "no";
        return $"status --porcelain=1 --untracked-files={untracked}";
    }

    private static IReadOnlyCollection<string> ParsePaths(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine;
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Contains('\t', StringComparison.Ordinal))
            {
                string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    paths.Add(parts[^1]);
                }

                continue;
            }

            string payload = line;
            if (line.Length >= 3 && line[2] == ' ')
            {
                payload = line[3..];
            }
            else if (line.Length >= 2 && char.IsWhiteSpace(line[1]))
            {
                payload = line[2..];
            }
            else if (line.Length > 0 && char.IsWhiteSpace(line[0]))
            {
                payload = line.TrimStart();
            }

            payload = payload.Trim();

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            if (payload.Contains(" -> ", StringComparison.Ordinal))
            {
                string[] renameParts = payload.Split(" -> ", StringSplitOptions.TrimEntries);
                if (renameParts.Length > 0)
                {
                    paths.Add(renameParts[^1]);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(payload))
            {
                paths.Add(payload);
            }
        }

        return paths.ToArray();
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
