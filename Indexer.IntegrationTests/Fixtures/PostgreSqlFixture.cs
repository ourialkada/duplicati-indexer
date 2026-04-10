using Testcontainers.PostgreSql;

namespace Indexer.IntegrationTests.Fixtures;

/// <summary>
/// Fixture that provides a PostgreSQL container for integration tests.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;

    /// <summary>
    /// Gets the connection string to the PostgreSQL database.
    /// </summary>
    public string ConnectionString => _postgresContainer.GetConnectionString();

    public PostgreSqlFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("indexer_test")
            .WithUsername("postgres")
            .WithPassword("testpassword")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    /// <summary>
    /// Starts the PostgreSQL container.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }
}
