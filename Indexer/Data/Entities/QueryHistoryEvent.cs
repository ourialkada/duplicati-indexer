namespace DuplicatiIndexer.Data.Entities;

/// <summary>
/// Represents a streaming event (like a thought or search action) executed during a RAG query process.
/// </summary>
public class QueryHistoryEvent
{
    /// <summary>
    /// Gets or sets the original type of the event, for example "thought" or "action".
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptive content of the event.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
