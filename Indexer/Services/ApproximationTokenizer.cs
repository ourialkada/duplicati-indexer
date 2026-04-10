using DuplicatiIndexer.AdapterInterfaces;
using DuplicatiIndexer.Configuration;
using System.Text;

namespace DuplicatiIndexer.Services;

/// <summary>
/// A simple tokenizer that approximates token counts using character-to-token ratio.
/// Default ratio is 4 characters per token, which is a reasonable approximation for English text.
/// </summary>
public class ApproximationTokenizer : ITokenizer
{
    private readonly double _charsPerToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproximationTokenizer"/> class.
    /// </summary>
    /// <param name="config">The environment configuration.</param>
    public ApproximationTokenizer(EnvironmentConfig config)
    {
        _charsPerToken = config.Chunking.CharsPerToken;

        if (_charsPerToken <= 0)
        {
            throw new ArgumentException("CharsPerToken must be greater than 0", nameof(config));
        }
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        // Simple approximation: tokens ≈ characters / charsPerToken
        // Ceiling to ensure we don't underestimate
        return (int)Math.Ceiling(text.Length / _charsPerToken);
    }

    /// <inheritdoc />
    public string TruncateToTokens(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return string.Empty;
        }

        var maxChars = (int)(maxTokens * _charsPerToken);

        if (text.Length <= maxChars)
        {
            return text;
        }

        return text.Substring(0, maxChars);
    }
}
