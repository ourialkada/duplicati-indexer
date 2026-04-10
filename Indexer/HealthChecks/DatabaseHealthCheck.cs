using Marten;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for PostgreSQL database connectivity via Marten.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDocumentStore _store;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="store">The Marten document store.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseHealthCheck(IDocumentStore store, ILogger<DatabaseHealthCheck> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var session = _store.QuerySession();
            // Try to execute a simple query to verify connectivity
            using var command = session.Connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is reachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database is not reachable", ex);
        }
    }
}
