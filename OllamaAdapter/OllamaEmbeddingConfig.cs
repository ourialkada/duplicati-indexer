namespace DuplicatiIndexer.OllamaAdapter;

/// <summary>
/// Configuration for Ollama embedding service.
/// </summary>
public class OllamaEmbeddingConfig
{
    /// <summary>
    /// Base URL for the Ollama API.
    /// Environment variable: INDEXER__OLLAMAEMBED__BASEURL
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name for embeddings.
    /// Environment variable: INDEXER__OLLAMAEMBED__MODEL
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";
}
