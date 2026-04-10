using DuplicatiIndexer.Data.Entities;
using DuplicatiIndexer.Messages;
using DuplicatiIndexer.Services;
using Marten;
using Wolverine;
using Wolverine.Attributes;

namespace DuplicatiIndexer.Handlers;

/// <summary>
/// Handler for processing DlistProcessingCompleted messages.
/// Filters filenames to find files that need indexing and triggers restoration.
/// </summary>
public class DlistProcessingCompletedHandler
{
    private readonly FilenameFilterService _filenameFilterService;
    private readonly IDocumentSession _session;
    private readonly ILogger<DlistProcessingCompletedHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DlistProcessingCompletedHandler"/> class.
    /// </summary>
    /// <param name="filenameFilterService">The filename filter service.</param>
    /// <param name="session">The Marten document session.</param>
    /// <param name="logger">The logger.</param>
    public DlistProcessingCompletedHandler(
        FilenameFilterService filenameFilterService,
        IDocumentSession session,
        ILogger<DlistProcessingCompletedHandler> logger)
    {
        _filenameFilterService = filenameFilterService;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Handles a DlistProcessingCompleted message by filtering files and triggering restoration.
    /// </summary>
    /// <param name="message">The DlistProcessingCompleted message.</param>
    /// <param name="bus">The message bus for publishing follow-up messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [WolverineHandler]
    public async Task Handle(DlistProcessingCompleted message, IMessageBus bus, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received DlistProcessingCompleted message for BackupId: {BackupId}, Version: {Version}, NewFilesAdded: {NewFilesAdded}",
            message.BackupId, message.Version, message.NewFilesAdded);

        try
        {
            // Get files that need indexing based on the filename filter
            var filesToIndex = await _filenameFilterService.GetFilesNeedingIndexingAsync(
                message.BackupSourceId,
                message.Version,
                cancellationToken);

            if (filesToIndex.Count == 0)
            {
                _logger.LogInformation(
                    "No indexable files found for BackupId: {BackupId}, Version: {Version}",
                    message.BackupId, message.Version);
                return;
            }

            // Get backup source configuration for restoration
            var backupSource = await _session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.Id == message.BackupSourceId, cancellationToken);

            if (backupSource == null)
            {
                _logger.LogError(
                    "BackupSource not found for BackupSourceId: {BackupSourceId}",
                    message.BackupSourceId);
                throw new InvalidOperationException(
                    $"BackupSource not found for BackupSourceId: {message.BackupSourceId}");
            }

            // Create a temporary directory for restoration
            var tempDir = Path.Combine(Path.GetTempPath(), "duplicati-restore", $"{message.BackupId}-{message.Version:yyyyMMddTHHmmss}");
            Directory.CreateDirectory(tempDir);

            _logger.LogInformation(
                "Publishing StartFileRestoration message for {FileCount} files to be restored to {TargetDirectory}",
                filesToIndex.Count, tempDir);

            // Extract file paths from the file entries
            var filePaths = filesToIndex.Select(f => f.Path).ToList();

            // Publish message to start file restoration
            await bus.PublishAsync(new StartFileRestoration
            {
                BackupId = message.BackupId,
                BackupSourceId = message.BackupSourceId,
                Version = message.Version,
                FilePaths = filePaths,
                TargetDirectory = tempDir
            });

            _logger.LogInformation(
                "Published StartFileRestoration message for BackupId: {BackupId}, Version: {Version}, Files: {FileCount}",
                message.BackupId, message.Version, filesToIndex.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing DlistProcessingCompleted message for BackupId: {BackupId}, Version: {Version}",
                message.BackupId, message.Version);
            throw;
        }
    }
}
