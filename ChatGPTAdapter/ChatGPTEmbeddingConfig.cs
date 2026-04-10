namespace DuplicatiIndexer.ChatGPTAdapter;

/// <summary>
/// Configuration for ChatGPT Embedding provider.
/// </summary>
public class ChatGPTEmbeddingConfig
{
    /// <summary>
    /// API key for ChatGPT.
    /// Environment variable: INDEXER__CHATGPT__APIKEY
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the OpenAI compatible API. Default is the official OpenAI endpoint.
    /// Environment variable: INDEXER__CHATGPT__BASEURL
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// Embed model name to use for ChatGPT.
    /// Environment variable: INDEXER__CHATGPT__EMBEDMODEL
    /// </summary>
    public string EmbedModel { get; set; } = "text-embedding-nomic-embed-text-v1.5";
}
