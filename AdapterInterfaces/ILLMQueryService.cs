namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Represents a single entry in the conversation history.
/// </summary>
public class ConversationHistoryItem
{
    /// <summary>
    /// Gets or sets the query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response text.
    /// </summary>
    public string Response { get; set; } = string.Empty;
}

/// <summary>
/// Service for generating answers using a Large Language Model.
/// </summary>
public interface ILLMQueryService
{
    /// <summary>
    /// Generates an answer to a query based on the provided context.
    /// </summary>
    /// <param name="query">The question to answer.</param>
    /// <param name="context">The context to base the answer on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated answer.</returns>
    Task<string> GenerateAnswerAsync(string query, string context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Condenses a follow-up query into a standalone query using conversation history.
    /// </summary>
    /// <param name="history">The conversation history containing previous queries and responses.</param>
    /// <param name="currentMessage">The follow-up query to condense.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The condensed standalone query.</returns>
    Task<string> CondenseQueryAsync(IEnumerable<ConversationHistoryItem> history, string currentMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a title for a session based on the initial query.
    /// </summary>
    /// <param name="query">The initial query to base the title on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated session title.</returns>
    Task<string> GenerateSessionTitleAsync(string query, CancellationToken cancellationToken = default);
}
