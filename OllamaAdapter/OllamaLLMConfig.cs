namespace DuplicatiIndexer.OllamaAdapter;

/// <summary>
/// Configuration for Ollama LLM provider.
/// </summary>
public class OllamaLLMConfig
{
    /// <summary>
    /// Base URL for the Ollama API.
    /// Environment variable: INDEXER__OLLAMALLM__BASEURL
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name to use for chat completions.
    /// Environment variable: INDEXER__OLLAMALLM__MODEL
    /// </summary>
    public string Model { get; set; } = "llama3.2";
}
