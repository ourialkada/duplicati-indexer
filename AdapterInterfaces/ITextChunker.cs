namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Represents a chunk of text with its metadata.
/// </summary>
public class TextChunk
{
    /// <summary>
    /// The chunk content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The index of this chunk in the sequence (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The total number of chunks for this document.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Character offset where this chunk starts in the original text.
    /// </summary>
    public int StartOffset { get; set; }

    /// <summary>
    /// Character offset where this chunk ends in the original text.
    /// </summary>
    public int EndOffset { get; set; }
}

/// <summary>
/// Service for chunking text into smaller pieces suitable for embedding.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits the input text into chunks that do not exceed the maximum chunk size.
    /// </summary>
    /// <param name="text">The text to chunk.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of text chunks with metadata.</returns>
    Task<IReadOnlyList<TextChunk>> ChunkTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the maximum chunk size in characters.
    /// </summary>
    int MaxChunkSize { get; }
}
