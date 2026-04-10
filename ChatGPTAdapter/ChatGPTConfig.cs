namespace DuplicatiIndexer.ChatGPTAdapter;

/// <summary>
/// Configuration for ChatGPT LLM provider.
/// </summary>
public class ChatGPTConfig
{
    /// <summary>
    /// Base URL for the OpenAI compatible API. Default is the official OpenAI endpoint.
    /// Environment variable: INDEXER__CHATGPT__BASEURL
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// API key for ChatGPT.
    /// Environment variable: INDEXER__CHATGPT__APIKEY
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name to use for ChatGPT.
    /// Environment variable: INDEXER__CHATGPT__MODEL
    /// </summary>
    public string Model { get; set; } = "gpt-5-mini";
}
