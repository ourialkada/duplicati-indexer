using DuplicatiIndexer.Data.Entities;
using Marten;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Service for managing RAG query sessions and history.
/// Provides common session operations used by both V1 and V2 RAG query services.
/// </summary>
public class RagQuerySessionService
{
    private readonly IDocumentSession _documentSession;
    private readonly ILogger<RagQuerySessionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagQuerySessionService"/> class.
    /// </summary>
    /// <param name="documentSession">The Marten document session for database operations.</param>
    /// <param name="logger">The logger.</param>
    public RagQuerySessionService(
        IDocumentSession documentSession,
        ILogger<RagQuerySessionService> logger)
    {
        _documentSession = documentSession;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a query session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="title">The title for new sessions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the session and a boolean indicating if it's new.</returns>
    public async Task<(QuerySession Session, bool IsNew)> GetOrCreateSessionAsync(
        Guid sessionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var session = await _documentSession.LoadAsync<QuerySession>(sessionId, cancellationToken);

        if (session != null)
        {
            // Update last activity timestamp
            session.LastActivityAt = DateTimeOffset.UtcNow;
            _documentSession.Store(session);
            return (session, false);
        }

        // Create new session
        var queryTimestamp = DateTimeOffset.UtcNow;
        session = new QuerySession
        {
            Id = sessionId,
            Title = title,
            CreatedAt = queryTimestamp,
            LastActivityAt = queryTimestamp
        };

        _documentSession.Store(session);
        _logger.LogInformation("Created new session {SessionId} with title: {Title}", sessionId, title);

        return (session, true);
    }

    /// <summary>
    /// Records a query and its response in the session history.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="originalQuery">The original query text.</param>
    /// <param name="condensedQuery">The condensed/standalone query.</param>
    /// <param name="response">The response/answer.</param>
    /// <param name="events">Optional list of events associated with the query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created history item.</returns>
    public async Task<QueryHistoryItem> RecordQueryAsync(
        Guid sessionId,
        string originalQuery,
        string condensedQuery,
        string response,
        List<QueryHistoryEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        var queryTimestamp = DateTimeOffset.UtcNow;
        var responseTimestamp = DateTimeOffset.UtcNow;

        var historyItem = new QueryHistoryItem
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OriginalQuery = originalQuery,
            CondensedQuery = condensedQuery,
            Response = response,
            QueryTimestamp = queryTimestamp,
            ResponseTimestamp = responseTimestamp,
            Events = events ?? new List<QueryHistoryEvent>()
        };

        _documentSession.Store(historyItem);
        await _documentSession.SaveChangesAsync(cancellationToken);

        return historyItem;
    }

    /// <summary>
    /// Gets the query history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of query history items for the session.</returns>
    public async Task<IReadOnlyList<QueryHistoryItem>> GetSessionHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _documentSession.Query<QueryHistoryItem>()
            .Where(h => h.SessionId == sessionId)
            .OrderBy(h => h.QueryTimestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a query session by ID.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query session, or null if not found.</returns>
    public async Task<QuerySession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _documentSession.LoadAsync<QuerySession>(sessionId, cancellationToken);
    }

    /// <summary>
    /// Gets the previous query history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of query history items.</returns>
    public async Task<IReadOnlyList<QueryHistoryItem>> GetHistoryForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _documentSession.Query<QueryHistoryItem>()
            .Where(h => h.SessionId == sessionId)
            .OrderBy(h => h.QueryTimestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all query sessions ordered by most recent activity.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all query sessions.</returns>
    public async Task<IReadOnlyList<QuerySession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await _documentSession.Query<QuerySession>()
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);
    }
}
