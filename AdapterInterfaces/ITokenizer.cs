namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Service for counting tokens in text.
/// Used to approximate token counts for embedding service limits.
/// </summary>
public interface ITokenizer
{
    /// <summary>
    /// Counts the approximate number of tokens in the given text.
    /// </summary>
    /// <param name="text">The text to count tokens for.</param>
    /// <returns>The approximate token count.</returns>
    int CountTokens(string text);

    /// <summary>
    /// Truncates text to fit within the specified token limit.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxTokens">The maximum number of tokens allowed.</param>
    /// <returns>The truncated text that fits within the token limit.</returns>
    string TruncateToTokens(string text, int maxTokens);
}
