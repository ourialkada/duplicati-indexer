namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Implements Reciprocal Rank Fusion (RRF) for combining search results from multiple sources.
/// RRF is a method for fusing results from different retrieval methods without requiring score normalization.
/// </summary>
internal static class RRF
{
    /// <summary>
    /// The default constant k used in the RRF formula: 1/(k + rank)
    /// A value of 60 is commonly used in literature and provides good balance
    /// between emphasizing top results and including lower-ranked items.
    /// </summary>
    public const int DefaultK = 60;

    /// <summary>
    /// Combines multiple result sets using Reciprocal Rank Fusion.
    /// </summary>
    /// <param name="resultSets">The result sets from different search methods.</param>
    /// <param name="k">The ranking constant. Higher values reduce the impact of rank differences.</param>
    /// <returns>A fused list of search results ordered by RRF score (descending).</returns>
    public static List<SearchResult> Fuse(IEnumerable<SearchResultSet> resultSets, int k = DefaultK)
    {
        // Dictionary to accumulate RRF scores by document ID
        var rrfScores = new Dictionary<string, double>();
        var resultLookup = new Dictionary<string, SearchResult>();

        foreach (var resultSet in resultSets)
        {
            foreach (var result in resultSet.Results)
            {
                var docId = result.Id;

                // Calculate RRF score contribution: 1 / (k + rank)
                var rrfScore = 1.0 / (k + result.Rank);

                if (rrfScores.ContainsKey(docId))
                {
                    rrfScores[docId] += rrfScore;
                }
                else
                {
                    rrfScores[docId] = rrfScore;
                    // Store the first occurrence for content/metadata
                    resultLookup[docId] = result;
                }
            }
        }

        // Create fused results sorted by RRF score
        var fusedResults = rrfScores
            .Select(pair => new SearchResult
            {
                Id = pair.Key,
                Content = resultLookup[pair.Key].Content,
                Score = pair.Value,
                Source = "hybrid",
                Metadata = new Dictionary<string, object>(resultLookup[pair.Key].Metadata)
                {
                    ["rrf_score"] = pair.Value,
                    ["sources"] = resultSets
                        .Where(rs => rs.Results.Any(r => r.Id == pair.Key))
                        .Select(rs => rs.SourceName)
                        .ToList()
                }
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        // Assign new ranks based on fused ordering
        for (int i = 0; i < fusedResults.Count; i++)
        {
            fusedResults[i].Rank = i + 1;
        }

        return fusedResults;
    }

    /// <summary>
    /// Combines two result sets using weighted Reciprocal Rank Fusion.
    /// Allows giving different weights to different search methods.
    /// </summary>
    /// <param name="resultSet1">The first result set with its weight.</param>
    /// <param name="weight1">The weight for the first result set.</param>
    /// <param name="resultSet2">The second result set with its weight.</param>
    /// <param name="weight2">The weight for the second result set.</param>
    /// <param name="k">The ranking constant.</param>
    /// <returns>A fused list of search results ordered by weighted RRF score.</returns>
    public static List<SearchResult> FuseWeighted(
        SearchResultSet resultSet1, double weight1,
        SearchResultSet resultSet2, double weight2,
        int k = DefaultK)
    {
        return FuseWeighted(new[] { (resultSet1, weight1), (resultSet2, weight2) }, k);
    }

    /// <summary>
    /// Combines multiple weighted result sets using weighted Reciprocal Rank Fusion.
    /// </summary>
    /// <param name="weightedResultSets">Tuples of result sets with their corresponding weights.</param>
    /// <param name="k">The ranking constant.</param>
    /// <returns>A fused list of search results ordered by weighted RRF score.</returns>
    public static List<SearchResult> FuseWeighted(
        IEnumerable<(SearchResultSet ResultSet, double Weight)> weightedResultSets,
        int k = DefaultK)
    {
        var rrfScores = new Dictionary<string, double>();
        var resultLookup = new Dictionary<string, SearchResult>();

        foreach (var (resultSet, weight) in weightedResultSets)
        {
            foreach (var result in resultSet.Results)
            {
                var docId = result.Id;
                // Calculate weighted RRF score contribution: weight * (1 / (k + rank))
                var rrfScore = weight * (1.0 / (k + result.Rank));

                if (rrfScores.ContainsKey(docId))
                {
                    rrfScores[docId] += rrfScore;
                }
                else
                {
                    rrfScores[docId] = rrfScore;
                    resultLookup[docId] = result;
                }
            }
        }

        var fusedResults = rrfScores
            .Select(pair => new SearchResult
            {
                Id = pair.Key,
                Content = resultLookup[pair.Key].Content,
                Score = pair.Value,
                Source = "hybrid_weighted",
                Metadata = new Dictionary<string, object>(resultLookup[pair.Key].Metadata)
                {
                    ["rrf_score"] = pair.Value
                }
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        for (int i = 0; i < fusedResults.Count; i++)
        {
            fusedResults[i].Rank = i + 1;
        }

        return fusedResults;
    }
}
