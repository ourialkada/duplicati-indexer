using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for Unstructured API connectivity.
/// </summary>
public class UnstructuredHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UnstructuredHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnstructuredHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public UnstructuredHealthCheck(HttpClient httpClient, ILogger<UnstructuredHealthCheck> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if the Unstructured API is healthy
            var response = await _httpClient.GetAsync("/healthcheck", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Unstructured API is reachable");
            }
            else
            {
                return HealthCheckResult.Degraded($"Unstructured API returned status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unstructured health check failed");
            return HealthCheckResult.Unhealthy("Unstructured API is not reachable", ex);
        }
    }
}
