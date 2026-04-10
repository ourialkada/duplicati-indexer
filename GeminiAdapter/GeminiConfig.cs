namespace DuplicatiIndexer.GeminiAdapter;

/// <summary>
/// Configuration for Gemini LLM provider.
/// </summary>
public class GeminiConfig
{
    /// <summary>
    /// API key for Gemini.
    /// Environment variable: INDEXER__GEMINI__APIKEY
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name to use for Gemini.
    /// Environment variable: INDEXER__GEMINI__MODEL
    /// </summary>
    public string Model { get; set; } = "gemini-3-flash-preview";
}
