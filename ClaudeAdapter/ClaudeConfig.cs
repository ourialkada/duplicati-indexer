namespace DuplicatiIndexer.ClaudeAdapter;

/// <summary>
/// Configuration for Claude LLM provider.
/// </summary>
public class ClaudeConfig
{
    /// <summary>
    /// API key for Claude.
    /// Environment variable: INDEXER__CLAUDE__APIKEY
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model name to use for Claude.
    /// Environment variable: INDEXER__CLAUDE__MODEL
    /// </summary>
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";

    /// <summary>
    /// Maximum tokens for Claude responses.
    /// Environment variable: INDEXER__CLAUDE__MAXTOKENS
    /// </summary>
    public int MaxTokens { get; set; } = 1024;
}
