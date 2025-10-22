using System;
using System.Threading;
using System.Threading.Tasks;
using Tester.FrameworkPackages;
using Utilities.ProcessRunner;
using Xunit;

namespace Tester.Utilities;

public sealed class ProcessRunnerTests
{
    private static string RepositoryRoot => RepositoryRootLocator.Resolve();

    [Fact]
    public async Task RunAsync_ProducesOutput()
    {
        ProcessRunner runner = new();
        ProcessSpec spec = new()
        {
            FileName = "dotnet",
            Arguments = "--version",
            WorkingDirectory = RepositoryRoot
        };

        ProcessResult result = await runner.RunAsync(spec, CancellationToken.None);

        Assert.True(result.CompletedSuccessfully);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task RunAsync_AppliesEnvironmentVariables()
    {
        const string token = "__WEBSTIR_PROCESS_RUNNER_TEST";
        ProcessRunner runner = new();
        ProcessSpec spec = new()
        {
            FileName = "node",
            Arguments = "-e \"console.log(process.env.__WEBSTIR_PROCESS_RUNNER_TEST ?? '')\"",
            WorkingDirectory = RepositoryRoot
        };
        spec.WithEnvironmentVariable(token, "hello-world");

        ProcessResult result = await runner.RunAsync(spec, CancellationToken.None);

        Assert.Contains("hello-world", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FlagsTimeouts()
    {
        ProcessRunner runner = new();
        ProcessSpec spec = new()
        {
            FileName = "node",
            Arguments = "-e \"setTimeout(() => {}, 5000);\"",
            WorkingDirectory = RepositoryRoot,
            ExitTimeout = TimeSpan.FromMilliseconds(100),
            TerminationMethod = TerminationMethod.Kill
        };

        ProcessResult result = await runner.RunAsync(spec, CancellationToken.None);

        Assert.True(result.TimedOut);
    }
}
