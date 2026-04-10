using DuplicatiIndexer.Data.Entities;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Calculates the difference between backup versions to determine which files need indexing.
/// </summary>
public class DiffCalculator
{
    private readonly ILogger<DiffCalculator> _logger;

    // Common indexable extensions
    private static readonly HashSet<string> IndexableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx", ".doc", ".rtf", ".csv", ".json", ".xml", ".html", ".htm"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffCalculator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DiffCalculator(ILogger<DiffCalculator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates which indexable files changed between two backup versions.
    /// </summary>
    /// <param name="entries">The source of backup file entries.</param>
    /// <param name="backupSourceId">The backup source identifier.</param>
    /// <param name="previousVersion">The previous backup version timestamp (used for logging context).</param>
    /// <param name="currentVersion">The current backup version timestamp to analyze.</param>
    /// <returns>A list of indexable files that were added or modified in the current version.</returns>
    public List<BackupFileEntry> CalculateDiff(
        IEnumerable<BackupFileEntry> entries,
        Guid backupSourceId,
        DateTimeOffset previousVersion,
        DateTimeOffset currentVersion)
    {
        _logger.LogInformation(
            "Calculating diff for BackupSourceId: {BackupSourceId} between versions {PreviousVersion} and {CurrentVersion}",
            backupSourceId, previousVersion, currentVersion);

        // Find files that were added or modified in the current version
        // A file is considered added/modified if its VersionAdded is the current version
        // We also want to make sure it hasn't been deleted in this version
        var filesToProcess = entries
            .Where(f => f.BackupSourceId == backupSourceId &&
                        f.VersionAdded == currentVersion &&
                        (f.VersionDeleted == null || f.VersionDeleted > currentVersion))
            .ToList();

        // Filter by indexable extensions
        var indexableFiles = filesToProcess
            .Where(f => IsIndexable(f.Path))
            .ToList();

        _logger.LogInformation("Found {Count} indexable files out of {TotalCount} changed files",
            indexableFiles.Count, filesToProcess.Count);

        return indexableFiles;
    }

    /// <summary>
    /// Determines if a file path has an indexable extension.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>True if the file is indexable; otherwise false.</returns>
    private bool IsIndexable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var extension = Path.GetExtension(path);
        return IndexableExtensions.Contains(extension);
    }
}
