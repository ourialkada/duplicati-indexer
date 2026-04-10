using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for Chroma vector database connectivity using v2 API.
/// </summary>
public class ChromaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChromaHealthCheck> _logger;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public ChromaHealthCheck(HttpClient httpClient, ILogger<ChromaHealthCheck> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        var connectionString = configuration["ConnectionStrings:Chroma"] ?? "http://localhost:8000";
        _baseUrl = connectionString.TrimEnd('/').Replace("/api/v1", "");
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to list collections as a health check
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/v2/tenants/default_tenant/databases/default_database/collections",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Chroma is reachable");
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return HealthCheckResult.Unhealthy($"Chroma returned {response.StatusCode}: {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chroma health check failed");
            return HealthCheckResult.Unhealthy("Chroma is not reachable", ex);
        }
    }
}
