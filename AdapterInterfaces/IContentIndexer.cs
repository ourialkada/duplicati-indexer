namespace DuplicatiIndexer.AdapterInterfaces;

/// <summary>
/// Service for extracting text content from files.
/// </summary>
public interface IContentIndexer
{
    /// <summary>
    /// Extracts text content from the specified file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The extracted text content.</returns>
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
}
