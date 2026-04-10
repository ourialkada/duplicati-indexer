using System.Globalization;
using System.Text.RegularExpressions;
using DuplicatiIndexer.Data.Entities;
using DuplicatiIndexer.Messages;
using DuplicatiIndexer.Services;
using Marten;
using Wolverine;
using Wolverine.Attributes;

namespace DuplicatiIndexer.Handlers;

/// <summary>
/// Handler for processing BackupVersionCreated messages.
/// </summary>
public class BackupVersionCreatedHandler
{
    private readonly DlistProcessor _dlistProcessor;
    private readonly BackendToolService _backendToolService;
    private readonly IDocumentSession _session;
    private readonly ILogger<BackupVersionCreatedHandler> _logger;

    // Regex to extract timestamp from dlist filename: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]
    private static readonly Regex DlistFilenameRegex = new(
        @"duplicati-(\d{8}T\d{6}Z)\.dlist",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupVersionCreatedHandler"/> class.
    /// </summary>
    /// <param name="dlistProcessor">The dlist processor.</param>
    /// <param name="backendToolService">The backend tool service for downloading files.</param>
    /// <param name="session">The Marten document session.</param>
    /// <param name="logger">The logger.</param>
    public BackupVersionCreatedHandler(
        DlistProcessor dlistProcessor,
        BackendToolService backendToolService,
        IDocumentSession session,
        ILogger<BackupVersionCreatedHandler> logger)
    {
        _dlistProcessor = dlistProcessor;
        _backendToolService = backendToolService;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Extracts the version timestamp from a dlist filename.
    /// </summary>
    /// <param name="dlistFilename">The dlist filename (e.g., "duplicati-20240312T143000Z.dlist.zip.aes").</param>
    /// <returns>The parsed DateTimeOffset.</returns>
    /// <exception cref="ArgumentException">Thrown when the filename format is invalid.</exception>
    public static DateTimeOffset ExtractVersionFromFilename(string dlistFilename)
    {
        var match = DlistFilenameRegex.Match(dlistFilename);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Invalid dlist filename format: '{dlistFilename}'. Expected format: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]",
                nameof(dlistFilename));
        }

        var timestampStr = match.Groups[1].Value;
        if (!DateTimeOffset.TryParseExact(
            timestampStr,
            "yyyyMMddTHHmmssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var version))
        {
            throw new ArgumentException(
                $"Failed to parse timestamp from dlist filename: '{dlistFilename}'",
                nameof(dlistFilename));
        }

        return version;
    }

    /// <summary>
    /// Handles a BackupVersionCreated message and processes the associated dlist file.
    /// Downloads the dlist file from the remote backend to a local temp path before processing.
    /// </summary>
    /// <param name="message">The BackupVersionCreated message.</param>
    /// <param name="bus">The message bus for publishing follow-up messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [WolverineHandler]
    public async Task Handle(BackupVersionCreated message, IMessageBus bus, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received BackupVersionCreated message for BackupId: {BackupId}, DlistFilename: {DlistFilename}",
            message.BackupId, message.DlistFilename);

        string? tempDlistPath = null;

        try
        {
            // Extract version from dlist filename
            var version = ExtractVersionFromFilename(message.DlistFilename);
            _logger.LogInformation("Extracted version {Version} from dlist filename", version);

            // Lookup the backup source to get passphrase and target URL
            var backupSource = await _session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == message.BackupId, cancellationToken);

            if (backupSource == null)
            {
                throw new InvalidOperationException(
                    $"BackupSource not found for BackupId: {message.BackupId}. " +
                    "Please ensure the backup source is configured with encryption password and target URL.");
            }

            if (string.IsNullOrEmpty(backupSource.TargetUrl))
            {
                throw new InvalidOperationException(
                    $"BackupSource {backupSource.Id} does not have a TargetUrl configured.");
            }

            // Download the dlist file from the remote backend to a local temp path.
            // The backend-tool GET command uses Path.GetFileName(localPath) as the remote filename,
            // so we create a temp directory and use the original filename within it.
            var tempDir = Path.Combine(Path.GetTempPath(), $"dlist-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            tempDlistPath = Path.Combine(tempDir, message.DlistFilename);

            _logger.LogInformation("Downloading dlist file {DlistFilename} from {TargetUrl} to {TempPath}",
                message.DlistFilename, backupSource.TargetUrl, tempDlistPath);

            await _backendToolService.DownloadFileAsync(
                backupSource.TargetUrl,
                message.DlistFilename,
                tempDlistPath,
                cancellationToken);

            _logger.LogInformation("Processing downloaded dlist file {TempDlistPath} for backup {BackupId} with version {Version}",
                tempDlistPath, message.BackupId, version);

            var result = await _dlistProcessor.ProcessDlistAsync(
                message.BackupId,
                version,
                tempDlistPath,
                backupSource.EncryptionPassword,
                cancellationToken
            );

            if (result.Success)
            {
                _logger.LogInformation("Successfully processed BackupVersionCreated message for BackupId: {BackupId}, Version: {Version}. New files added: {NewFiles}",
                    message.BackupId, version, result.NewFilesAdded);

                // Publish message to trigger filename filtering for new files
                if (result.NewFilesAdded > 0)
                {
                    await bus.PublishAsync(new DlistProcessingCompleted
                    {
                        BackupId = result.BackupId,
                        BackupSourceId = result.BackupSourceId,
                        Version = result.Version,
                        NewFilesAdded = result.NewFilesAdded
                    });

                    _logger.LogInformation("Published DlistProcessingCompleted message for BackupId: {BackupId}, Version: {Version}",
                        result.BackupId, result.Version);
                }
                else
                {
                    _logger.LogInformation("No new files added, skipping DlistProcessingCompleted message for BackupId: {BackupId}",
                        message.BackupId);
                }
            }
            else
            {
                _logger.LogError("Failed to process dlist file for BackupId: {BackupId}. Error: {Error}",
                    message.BackupId, result.ErrorMessage);
                throw new InvalidOperationException($"Dlist processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BackupVersionCreated message for BackupId: {BackupId}, DlistFilename: {DlistFilename}",
                message.BackupId, message.DlistFilename);

            // Re-throw to allow Wolverine to handle retry logic
            throw;
        }
        finally
        {
            // Clean up the temp dlist file and its directory
            if (tempDlistPath != null)
            {
                try
                {
                    var tempDir = Path.GetDirectoryName(tempDlistPath);
                    if (tempDir != null && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                        _logger.LogDebug("Cleaned up temp directory {TempDir}", tempDir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp dlist file {TempDlistPath}", tempDlistPath);
                }
            }
        }
    }
}
