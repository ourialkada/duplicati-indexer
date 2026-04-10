namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Represents a single search result with content, score, and metadata.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Gets or sets the unique identifier of the document/chunk.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the search result.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the search score (normalized 0-1, where higher is better).
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Gets or sets the rank of this result in its source list (1-based).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Gets or sets the source of this result (e.g., "vector", "sparse", "hybrid").
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata about the result.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Represents a collection of search results from a single search method.
/// </summary>
public class SearchResultSet
{
    /// <summary>
    /// Gets or sets the name of the search method that produced these results.
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the search results.
    /// </summary>
    public List<SearchResult> Results { get; set; } = new();

    /// <summary>
    /// Gets or sets the original query string.
    /// </summary>
    public string Query { get; set; } = string.Empty;
}
