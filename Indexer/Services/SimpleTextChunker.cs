using DuplicatiIndexer.AdapterInterfaces;
using DuplicatiIndexer.Configuration;
using System.Text;

namespace DuplicatiIndexer.Services;

/// <summary>
/// A simple text chunker that splits text by token count with optional overlap.
/// Attempts to split at word boundaries when possible.
/// </summary>
public class SimpleTextChunker : ITextChunker
{
    private readonly int _maxChunkTokens;
    private readonly int _overlapTokens;
    private readonly ITokenizer _tokenizer;

    /// <inheritdoc />
    public int MaxChunkSize => _maxChunkTokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleTextChunker"/> class.
    /// </summary>
    /// <param name="config">The environment configuration containing chunking settings.</param>
    /// <param name="tokenizer">The tokenizer for counting tokens.</param>
    public SimpleTextChunker(EnvironmentConfig config, ITokenizer tokenizer)
    {
        _maxChunkTokens = config.Chunking.MaxChunkSize;
        _overlapTokens = config.Chunking.OverlapSize;
        _tokenizer = tokenizer;

        // Validate configuration
        if (_maxChunkTokens <= 0)
        {
            throw new ArgumentException("MaxChunkSize must be greater than 0", nameof(config));
        }

        if (_overlapTokens < 0)
        {
            throw new ArgumentException("OverlapSize must be non-negative", nameof(config));
        }

        if (_overlapTokens >= _maxChunkTokens)
        {
            throw new ArgumentException("OverlapSize must be less than MaxChunkSize", nameof(config));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TextChunk>> ChunkTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult<IReadOnlyList<TextChunk>>(Array.Empty<TextChunk>());
        }

        var totalTokens = _tokenizer.CountTokens(text);

        // If text fits in a single chunk, return it as-is
        if (totalTokens <= _maxChunkTokens)
        {
            var singleChunk = new TextChunk
            {
                Content = text,
                Index = 0,
                TotalChunks = 1,
                StartOffset = 0,
                EndOffset = text.Length
            };
            return Task.FromResult<IReadOnlyList<TextChunk>>(new[] { singleChunk });
        }

        var chunks = new List<TextChunk>();
        int currentPosition = 0;
        int chunkIndex = 0;

        while (currentPosition < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Calculate the end position for this chunk based on token limit
            int remainingLength = text.Length - currentPosition;
            int searchEnd = currentPosition + remainingLength;

            // Binary search to find the optimal split point within token limit
            int bestSplit = FindOptimalSplitPoint(text, currentPosition, searchEnd, _maxChunkTokens);

            // Extract the chunk content
            int length = bestSplit - currentPosition;
            string chunkContent = text.Substring(currentPosition, length);

            chunks.Add(new TextChunk
            {
                Content = chunkContent,
                Index = chunkIndex,
                StartOffset = currentPosition,
                EndOffset = bestSplit
            });

            // Move to next position, accounting for overlap
            if (bestSplit >= text.Length)
            {
                break;
            }

            // Calculate overlap in characters based on token count
            int overlapChars = EstimateOverlapChars(text, bestSplit, _overlapTokens);
            currentPosition = bestSplit - overlapChars;

            if (currentPosition >= bestSplit)
            {
                // Prevent infinite loop if overlap calculation fails
                currentPosition = bestSplit;
            }

            chunkIndex++;

            // Safety check to prevent infinite loops
            if (chunkIndex > 10000)
            {
                throw new InvalidOperationException("Too many chunks generated. The text may be too large or the chunk size too small.");
            }
        }

        // Update total chunks count
        foreach (var chunk in chunks)
        {
            chunk.TotalChunks = chunks.Count;
        }

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }

    /// <summary>
    /// Finds the optimal split point within token limit, preferring word boundaries.
    /// Uses binary search for efficiency.
    /// </summary>
    private int FindOptimalSplitPoint(string text, int start, int end, int maxTokens)
    {
        // First, check if entire range fits
        var rangeText = text.Substring(start, end - start);
        if (_tokenizer.CountTokens(rangeText) <= maxTokens)
        {
            return end;
        }

        // Binary search for the split point
        int low = start;
        int high = end;
        int bestSplit = start;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var testText = text.Substring(start, mid - start);
            var tokens = _tokenizer.CountTokens(testText);

            if (tokens <= maxTokens)
            {
                bestSplit = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        // Try to find a word boundary near the split point
        return FindWordBoundary(text, start, bestSplit);
    }

    /// <summary>
    /// Finds a word boundary near the specified position by looking for whitespace.
    /// Searches backward up to 20% of estimated character range, then forward if necessary.
    /// </summary>
    private int FindWordBoundary(string text, int start, int position)
    {
        if (position <= start || position >= text.Length)
        {
            return position;
        }

        // Estimate how many characters 20% of max tokens would be
        int searchRange = (int)(_maxChunkTokens * 0.2 * 4); // Approx 4 chars per token
        int searchLimit = Math.Max(start, position - searchRange);

        // Look backward for whitespace
        for (int i = position; i > searchLimit; i--)
        {
            if (char.IsWhiteSpace(text[i - 1]))
            {
                return i;
            }
        }

        // If no whitespace found backward, look forward
        int forwardLimit = Math.Min(text.Length, position + (searchRange / 2));
        for (int i = position; i < forwardLimit; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        // If no word boundary found, return the original position
        return position;
    }

    /// <summary>
    /// Estimates the number of characters that correspond to the given token overlap.
    /// </summary>
    private int EstimateOverlapChars(string text, int endPosition, int overlapTokens)
    {
        if (overlapTokens <= 0 || endPosition <= 0)
        {
            return 0;
        }

        // Binary search backward to find the position that gives us approximately overlapTokens
        int low = Math.Max(0, endPosition - (overlapTokens * 8)); // Rough estimate: max 8 chars per token
        int high = endPosition;
        int bestPosition = endPosition;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var overlapText = text.Substring(mid, endPosition - mid);
            var tokens = _tokenizer.CountTokens(overlapText);

            if (tokens <= overlapTokens)
            {
                bestPosition = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return endPosition - bestPosition;
    }
}
