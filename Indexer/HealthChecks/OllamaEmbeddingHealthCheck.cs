using DuplicatiIndexer.OllamaAdapter;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for Ollama service.
/// </summary>
public class OllamaEmbeddingHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly OllamaEmbeddingConfig _config;
    private readonly ILogger<OllamaEmbeddingHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaEmbeddingHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="config">The Ollama embedding configuration.</param>
    /// <param name="logger">The logger.</param>
    public OllamaEmbeddingHealthCheck(HttpClient httpClient, OllamaEmbeddingConfig config, ILogger<OllamaEmbeddingHealthCheck> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/api/tags", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Ollama health check passed");
                return HealthCheckResult.Healthy("Ollama is running");
            }
            else
            {
                _logger.LogWarning("Ollama health check failed with status code {StatusCode}", response.StatusCode);
                return HealthCheckResult.Unhealthy($"Ollama returned status code {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama health check failed");
            return HealthCheckResult.Unhealthy($"Ollama health check failed: {ex.Message}");
        }
    }
}
