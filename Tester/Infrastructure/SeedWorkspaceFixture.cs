using System.Threading.Tasks;
using Xunit;

namespace Tester.Infrastructure;

public sealed class SeedWorkspaceFixture : IAsyncLifetime
{
    public SeedWorkspaceFixture()
    {
        Context = new TestCaseContext(new Runner(), Paths.OutPath);
    }

    public TestCaseContext Context
    {
        get;
    }

    public Task InitializeAsync()
    {
        WorkspaceManager.EnsureSeedWorkspaceReady(Context);
        WorkspaceManager.EnsureBackendFrameworkBuilt();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
