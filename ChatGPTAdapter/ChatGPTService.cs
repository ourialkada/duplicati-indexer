using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DuplicatiIndexer.AdapterInterfaces;
using Microsoft.Extensions.Logging;

namespace DuplicatiIndexer.ChatGPTAdapter;

/// <summary>
/// Implementation of ILLMClient using OpenAI's ChatGPT API.
/// </summary>
public class ChatGPTService : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly ChatGPTConfig _config;
    private readonly ILogger<ChatGPTService> _logger;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatGPTService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="config">The ChatGPT configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="InvalidOperationException">Thrown when ChatGPT API key is not configured.</exception>
    public ChatGPTService(HttpClient httpClient, ChatGPTConfig config, ILogger<ChatGPTService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _apiKey = !string.IsNullOrEmpty(_config.ApiKey)
            ? _config.ApiKey
            : "dummy-key"; // Default to a dummy key if none is provided (useful for local OpenAI compatible servers)

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        var url = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";

        var model = _config.Model;

        var messageList = messages.Select(m => new { role = ToRoleString(m.Role), content = m.Content }).ToArray();

        var requestBody = new
        {
            model = model,
            messages = messageList
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var content = responseContent.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        return content ?? string.Empty;
    }

    private static string ToRoleString(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), $"Unknown role: {role}")
    };
}
