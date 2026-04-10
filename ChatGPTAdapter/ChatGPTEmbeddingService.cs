using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DuplicatiIndexer.AdapterInterfaces;
using Microsoft.Extensions.Logging;

namespace DuplicatiIndexer.ChatGPTAdapter;

/// <summary>
/// Implementation of IEmbeddingService using OpenAI's compatible API.
/// </summary>
public class ChatGPTEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ChatGPTEmbeddingConfig _config;
    private readonly ILogger<ChatGPTEmbeddingService> _logger;
    private readonly string _apiKey;

    public ChatGPTEmbeddingService(HttpClient httpClient, ChatGPTEmbeddingConfig config, ILogger<ChatGPTEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _apiKey = !string.IsNullOrEmpty(_config.ApiKey)
            ? _config.ApiKey
            : "dummy-key";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        var url = $"{_config.BaseUrl.TrimEnd('/')}/embeddings";

        var requestBody = new
        {
            model = _config.EmbedModel,
            input = text
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorContent}", 
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        
        if (!responseContent.TryGetProperty("data", out var dataProperty))
        {
            throw new InvalidOperationException("Embedding response does not contain 'data' property.");
        }

        var embeddingProperty = dataProperty[0].GetProperty("embedding");
        var embedding = embeddingProperty.EnumerateArray().Select(x => x.GetSingle()).ToArray();

        return embedding;
    }
}
