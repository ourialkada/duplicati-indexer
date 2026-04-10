namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Result of a RAG query execution.
/// </summary>
public class RagQueryResult
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the original query.
    /// </summary>
    public string OriginalQuery { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the condensed/standalone query used for RAG search.
    /// </summary>
    public string CondensedQuery { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated answer.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is a new session.
    /// </summary>
    public bool IsNewSession { get; set; }

    /// <summary>
    /// Gets or sets the session title.
    /// </summary>
    public string? SessionTitle { get; set; }
}

/// <summary>
/// Event emitted during the RAG query process.
/// </summary>
public class RagQueryEvent
{
    /// <summary>
    /// Gets or sets the event type (e.g. "thought", "action", "observation", "error", "answer").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content or description of the event.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Additional context data, if any.
    /// </summary>
    public string? EventContext { get; set; }
}

/// <summary>
/// Interface for RAG (Retrieval-Augmented Generation) query services.
/// Implementations provide different approaches to querying indexed content.
/// </summary>
public interface IRagQueryService
{
    /// <summary>
    /// Executes a RAG query to find and answer questions based on indexed content.
    /// Manages session state including query history.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="query">The user's question.</param>
    /// <param name="topK">The number of similar documents to retrieve.</param>
    /// <param name="onEvent">Optional callback for stream progress events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query result containing the answer and session information.</returns>
    Task<RagQueryResult> QueryAsync(Guid sessionId, string query, int topK = 5, Func<RagQueryEvent, Task>? onEvent = null, CancellationToken cancellationToken = default);
}
