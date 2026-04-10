namespace DuplicatiIndexer.Data.Entities;

/// <summary>
/// Represents a job for indexing a backup file.
/// </summary>
public class IndexingJob
{
    /// <summary>
    /// Gets or sets the unique identifier for the indexing job.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the backup file entry to index.
    /// </summary>
    public Guid BackupFileEntryId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the indexing job.
    /// </summary>
    public IndexingStatus Status { get; set; } = IndexingStatus.Pending;

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job completed, if applicable.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
