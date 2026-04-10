namespace DuplicatiIndexer.Messages;

/// <summary>
/// Message instructing the restorer to restore files to a temporary folder for indexing.
/// </summary>
public record StartFileRestoration
{
    /// <summary>
    /// Gets the unique identifier for the backup (maps to BackupSource.DuplicatiBackupId).
    /// </summary>
    public required string BackupId { get; init; }

    /// <summary>
    /// Gets the backup source identifier.
    /// </summary>
    public required Guid BackupSourceId { get; init; }

    /// <summary>
    /// Gets the version timestamp to restore from.
    /// </summary>
    public required DateTimeOffset Version { get; init; }

    /// <summary>
    /// Gets the list of file paths to restore.
    /// </summary>
    public required List<string> FilePaths { get; init; }

    /// <summary>
    /// Gets the temporary directory where files should be restored.
    /// </summary>
    public required string TargetDirectory { get; init; }
}
