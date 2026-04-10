using System.Net.Http.Json;
using System.Text.Json;
using DuplicatiIndexer.AdapterInterfaces;
using Microsoft.Extensions.Logging;

namespace DuplicatiIndexer.OllamaAdapter;

/// <summary>
/// Implementation of IEmbeddingService using Ollama's local embedding API.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaEmbeddingConfig _config;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly string _model;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaEmbeddingService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="config">The Ollama embedding configuration.</param>
    /// <param name="logger">The logger.</param>
    public OllamaEmbeddingService(HttpClient httpClient, OllamaEmbeddingConfig config, ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _model = _config.Model;
        _baseUrl = _config.BaseUrl;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        _logger.LogDebug("Generating embedding using Ollama model {Model}", _model);

        var url = $"{_baseUrl}/api/embeddings";

        var requestBody = new
        {
            model = _model,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Ollama embedding request failed with status {StatusCode}: {ErrorContent}", 
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        
        if (!responseContent.TryGetProperty("embedding", out var embeddingProperty))
        {
            throw new InvalidOperationException("Ollama response does not contain 'embedding' property.");
        }

        var embedding = embeddingProperty.EnumerateArray().Select(x => x.GetSingle()).ToArray();

        _logger.LogDebug("Successfully generated embedding with {DimensionCount} dimensions using Ollama", embedding.Length);

        return embedding;
    }
}
