using DuplicatiIndexer.AdapterInterfaces;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Implementation of ILLMQueryService that contains the business logic and prompt templates.
/// Delegates to ILLMClient for actual LLM communication.
/// </summary>
public class LLMQueryService : ILLMQueryService
{
    private readonly ILLMClient _llmClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMQueryService"/> class.
    /// </summary>
    /// <param name="llmClient">The LLM client for making completions.</param>
    public LLMQueryService(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAnswerAsync(string query, string context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty.", nameof(query));
        }

        if (string.IsNullOrWhiteSpace(context))
        {
            throw new ArgumentException("Context cannot be null or empty.", nameof(context));
        }

        var messages = new[]
        {
            new ChatMessage
            {
                Role = ChatRole.System,
                Content = "You are a helpful assistant answering questions based on the provided context."
            },
            new ChatMessage
            {
                Role = ChatRole.User,
                Content = $"Context:\n{context}\n\nQuestion:\n{query}\n\nAnswer:"
            }
        };

        return await _llmClient.CompleteAsync(messages, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> CondenseQueryAsync(IEnumerable<ConversationHistoryItem> history, string currentMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentMessage))
        {
            throw new ArgumentException("Current message cannot be null or empty.", nameof(currentMessage));
        }

        var historyText = string.Join("\n", history.Select(h => $"Q: {h.Query}\nA: {h.Response}"));

        var messages = new[]
        {
            new ChatMessage
            {
                Role = ChatRole.System,
                Content = "You are a helpful assistant that rephrases follow-up questions into standalone questions based on conversation history."
            },
            new ChatMessage
            {
                Role = ChatRole.User,
                Content = $"History:\n{historyText}\n\nFollow-up: {currentMessage}\n\nStandalone question:"
            }
        };

        var condensedQuery = await _llmClient.CompleteAsync(messages, cancellationToken);
        return condensedQuery?.Trim() ?? currentMessage;
    }

    /// <inheritdoc />
    public async Task<string> GenerateSessionTitleAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty.", nameof(query));
        }

        var messages = new[]
        {
            new ChatMessage
            {
                Role = ChatRole.System,
                Content = "You are a helpful assistant that generates short, concise titles (maximum 50 characters) for conversation sessions. The title should summarize the main topic."
            },
            new ChatMessage
            {
                Role = ChatRole.User,
                Content = $"Query: {query}\n\nTitle:"
            }
        };

        var title = await _llmClient.CompleteAsync(messages, cancellationToken);
        return title?.Trim() ?? "New Session";
    }
}
