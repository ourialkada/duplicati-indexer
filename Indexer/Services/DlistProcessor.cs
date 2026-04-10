using System.Diagnostics;
using Duplicati.Library.Compression.ZipCompression;
using Duplicati.Library.Encryption;
using Duplicati.Library.Interface;
using Duplicati.Library.Main;
using Duplicati.Library.Main.Volumes;
using DuplicatiIndexer.Data.Entities;
using Marten;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Result of processing a dlist file.
/// </summary>
public class DlistProcessingResult
{
    /// <summary>
    /// Gets or sets a value indicating whether processing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the backup identifier.
    /// </summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backup source identifier.
    /// </summary>
    public Guid BackupSourceId { get; set; }

    /// <summary>
    /// Gets or sets the version timestamp.
    /// </summary>
    public DateTimeOffset Version { get; set; }

    /// <summary>
    /// Gets or sets the number of new file entries added.
    /// </summary>
    public long NewFilesAdded { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Processes Duplicati dlist files to extract file metadata.
/// </summary>
public class DlistProcessor
{
    private readonly IDocumentSession _session;
    private readonly ILogger<DlistProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DlistProcessor"/> class.
    /// </summary>
    /// <param name="session">The Marten document session.</param>
    /// <param name="logger">The logger.</param>
    public DlistProcessor(IDocumentSession session, ILogger<DlistProcessor> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Processes a dlist file and stores file entries in the database.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="version">The backup version timestamp extracted from the dlist filename.</param>
    /// <param name="dlistFilePath">The path to the dlist file.</param>
    /// <param name="passphrase">The encryption passphrase, if the file is encrypted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing information about the processing operation.</returns>
    public async Task<DlistProcessingResult> ProcessDlistAsync(string backupId, DateTimeOffset version, string dlistFilePath, string? passphrase, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing dlist file {DlistFilePath} for backup {BackupId} version {Version}", dlistFilePath, backupId, version);

        if (!File.Exists(dlistFilePath))
        {
            _logger.LogError("Dlist file not found: {DlistFilePath}", dlistFilePath);
            return new DlistProcessingResult
            {
                Success = false,
                BackupId = backupId,
                Version = version,
                ErrorMessage = $"Dlist file not found: {dlistFilePath}"
            };
        }

        string? tempDecryptedFile = null;
        string fileToProcess = dlistFilePath;
        Guid backupSourceId = Guid.Empty;
        long totalEntries = 0;

        try
        {
            if (dlistFilePath.EndsWith(".aes", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    _logger.LogError("Passphrase required to decrypt {DlistFilePath}", dlistFilePath);
                    return new DlistProcessingResult
                    {
                        Success = false,
                        BackupId = backupId,
                        Version = version,
                        ErrorMessage = $"Passphrase required to decrypt {dlistFilePath}"
                    };
                }

                tempDecryptedFile = Path.GetTempFileName();
                _logger.LogInformation("Decrypting {DlistFilePath} to {TempFile}", dlistFilePath, tempDecryptedFile);

                using var aes = new AESEncryption(passphrase, new Dictionary<string, string>());
                aes.Decrypt(dlistFilePath, tempDecryptedFile);
                fileToProcess = tempDecryptedFile;
            }

            var options = new Options(new Dictionary<string, string?>());

            using var fileStream = new FileStream(fileToProcess, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new FileArchiveZip(fileStream, ArchiveMode.Read, new Dictionary<string, string?>());
            using var reader = new FilesetVolumeReader(zip, options);

            var backupSource = await _session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId, cancellationToken);
            if (backupSource == null)
            {
                throw new InvalidOperationException(
                    $"BackupSource not found for BackupId: {backupId}. " +
                    "Please ensure the backup source is configured with encryption password and target URL.");
            }

            backupSourceId = backupSource.Id;

            const int batchSize = 1000;
            var fileEntryBatch = new List<BackupFileEntry>();
            var versionFileBatch = new List<BackupVersionFile>();
            long totalEntriesLong = 0;
            long skippedEntries = 0;

            // Collect all file keys from the dlist to check for duplicates
            var dlistFiles = reader.Files
                .Where(f => f.Type == FilelistEntryType.File)
                .Select(f => new { f.Path, f.Hash, f.Size, f.Time })
                .ToList();

            // Build file keys for batch existence checks
            var fileKeysToCheck = dlistFiles
                .Select(f => $"{f.Path}|{f.Hash}")
                .ToList();

            // Check existence in batches using a more efficient query
            var existingFileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const int checkBatchSize = 500;

            for (int i = 0; i < fileKeysToCheck.Count; i += checkBatchSize)
            {
                var batchKeys = fileKeysToCheck.Skip(i).Take(checkBatchSize).ToList();
                var batchPaths = dlistFiles.Skip(i).Take(checkBatchSize).Select(f => f.Path).ToList();

                // Query only for files with matching paths in this batch
                var existingBatch = await _session.Query<BackupFileEntry>()
                    .Where(f => f.BackupSourceId == backupSource.Id
                                && f.VersionDeleted == null
                                && batchPaths.Contains(f.Path))
                    .Select(f => new { f.Path, f.Hash })
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingBatch)
                {
                    existingFileKeys.Add($"{existing.Path}|{existing.Hash}");
                }
            }

            foreach (var file in dlistFiles)
            {
                var fileKey = $"{file.Path}|{file.Hash}";

                // Always record the file presence in this backup version
                versionFileBatch.Add(new BackupVersionFile
                {
                    Id = Guid.NewGuid(),
                    BackupSourceId = backupSource.Id,
                    Version = version,
                    Path = file.Path,
                    Hash = file.Hash,
                    Size = file.Size,
                    LastModified = file.Time.ToUniversalTime(),
                    RecordedAt = DateTimeOffset.UtcNow
                });

                // For BackupFileEntry, skip if this exact file (same path and hash) already exists
                if (existingFileKeys.Contains(fileKey))
                {
                    skippedEntries++;
                }
                else
                {
                    fileEntryBatch.Add(new BackupFileEntry
                    {
                        Id = Guid.NewGuid(),
                        BackupSourceId = backupSource.Id,
                        VersionAdded = version,
                        Path = file.Path,
                        Hash = file.Hash,
                        Size = file.Size,
                        LastModified = file.Time.ToUniversalTime()
                    });

                    // Add to tracking set to avoid duplicates within the same batch
                    existingFileKeys.Add(fileKey);
                }

                // Save batches when they reach the size limit
                if (fileEntryBatch.Count >= batchSize)
                {
                    _session.StoreObjects(fileEntryBatch);
                    totalEntriesLong += fileEntryBatch.Count;
                    fileEntryBatch.Clear();
                }

                if (versionFileBatch.Count >= batchSize)
                {
                    _session.StoreObjects(versionFileBatch);
                    await _session.SaveChangesAsync(cancellationToken);
                    versionFileBatch.Clear();
                }
            }

            // Insert remaining entries
            if (fileEntryBatch.Count > 0)
            {
                _session.StoreObjects(fileEntryBatch);
                totalEntriesLong += fileEntryBatch.Count;
            }

            if (versionFileBatch.Count > 0)
            {
                _session.StoreObjects(versionFileBatch);
            }

            // Save any remaining changes
            await _session.SaveChangesAsync(cancellationToken);

            totalEntries = (int)totalEntriesLong;

            if (skippedEntries > 0)
            {
                _logger.LogInformation("Skipped {SkippedCount} duplicate file entries in {DlistFilePath} (stored in BackupVersionFile)",
                    skippedEntries, dlistFilePath);
            }

            _logger.LogInformation("Found {Count} file entries in {DlistFilePath}", totalEntries, dlistFilePath);

            // Update BackupSource LastParsedVersion
            if (backupSource.LastParsedVersion == null || version > backupSource.LastParsedVersion)
            {
                backupSource.LastParsedVersion = version;
            }

            _session.Store(backupSource);
            await _session.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully processed dlist file {DlistFilePath}", dlistFilePath);

            return new DlistProcessingResult
            {
                Success = true,
                BackupId = backupId,
                BackupSourceId = backupSourceId,
                Version = version,
                NewFilesAdded = totalEntries
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing dlist file {DlistFilePath} after {ElapsedMs}ms", dlistFilePath, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            if (tempDecryptedFile != null && File.Exists(tempDecryptedFile))
            {
                try
                {
                    File.Delete(tempDecryptedFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary decrypted file {TempFile}", tempDecryptedFile);
                }
            }
        }
    }
}
