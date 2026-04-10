using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for Qdrant vector database connectivity.
/// </summary>
public class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantHealthCheck"/> class.
    /// </summary>
    /// <param name="client">The Qdrant client.</param>
    /// <param name="logger">The logger.</param>
    public QdrantHealthCheck(QdrantClient client, ILogger<QdrantHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get cluster info as a health check
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            return HealthCheckResult.Healthy("Qdrant is reachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant health check failed");
            return HealthCheckResult.Unhealthy("Qdrant is not reachable", ex);
        }
    }
}
