using Xunit;

namespace Tester.Infrastructure;

[CollectionDefinition(CollectionName)]
public sealed class SeedWorkspaceCollection : ICollectionFixture<SeedWorkspaceFixture>
{
    public const string CollectionName = "SeedWorkspace";
}
