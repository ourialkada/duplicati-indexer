namespace Indexer.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for integration tests that use Docker containers.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<QdrantFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
