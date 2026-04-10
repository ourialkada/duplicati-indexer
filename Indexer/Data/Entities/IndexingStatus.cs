namespace DuplicatiIndexer.Data.Entities;

/// <summary>
/// Represents the status of an indexing job.
/// </summary>
public enum IndexingStatus
{
    /// <summary>
    /// The job is pending and waiting to be processed.
    /// </summary>
    Pending,

    /// <summary>
    /// The job is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// The job has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The job has failed.
    /// </summary>
    Failed
}
