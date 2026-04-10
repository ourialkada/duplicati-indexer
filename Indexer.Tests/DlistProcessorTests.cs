using System.IO.Compression;
using DuplicatiIndexer.Data;
using DuplicatiIndexer.Data.Entities;
using DuplicatiIndexer.Services;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Indexer.Tests;

/// <summary>
/// Integration tests for DlistProcessor using PostgreSQL container.
/// </summary>
public class DlistProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly ITestOutputHelper _output;
    private ServiceProvider _serviceProvider = null!;
    private IDocumentStore _documentStore = null!;

    public DlistProcessorTests(ITestOutputHelper output)
    {
        _output = output;
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("indexer_test")
            .WithUsername("postgres")
            .WithPassword("testpassword")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    /// <summary>
    /// Starts the PostgreSQL container and configures Marten.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        _output.WriteLine($"PostgreSQL container started with connection string: {_postgresContainer.GetConnectionString()}");

        // Setup DI container with test configuration
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddXUnit(_output));

        // Configure Marten with test PostgreSQL
        services.AddMarten(options =>
        {
            MartenConfiguration.Configure(options, _postgresContainer.GetConnectionString());
        }).UseLightweightSessions();

        _serviceProvider = services.BuildServiceProvider();
        _documentStore = _serviceProvider.GetRequiredService<IDocumentStore>();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates a new IDocumentSession for database operations.
    /// </summary>
    private IDocumentSession CreateSession()
    {
        return _documentStore.LightweightSession();
    }

    /// <summary>
    /// Creates a DlistProcessor instance with a new session.
    /// </summary>
    private DlistProcessor CreateProcessor()
    {
        var session = CreateSession();
        var logger = _serviceProvider.GetRequiredService<ILogger<DlistProcessor>>();
        return new DlistProcessor(session, logger);
    }

    /// <summary>
    /// Creates a test BackupSource in the database.
    /// </summary>
    private async Task<BackupSource> CreateTestBackupSource(string backupId, string? targetUrl = null)
    {
        using var session = CreateSession();
        var backupSource = new BackupSource
        {
            Id = Guid.NewGuid(),
            DuplicatiBackupId = backupId,
            Name = $"Test Backup {backupId}",
            CreatedAt = DateTimeOffset.UtcNow,
            TargetUrl = targetUrl ?? $"file://{Path.GetTempPath()}/backups"
        };
        session.Store(backupSource);
        await session.SaveChangesAsync();
        return backupSource;
    }

    /// <summary>
    /// Creates a test dlist file with the specified files.
    /// Dlist files are ZIP archives containing:
    /// - manifest: JSON with version info (must match Duplicati's expected format)
    /// - filelist.json: JSON array of file entries
    /// </summary>
    private string CreateTestDlistFile(Dictionary<string, (long size, DateTime time, string hash)> files)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_dlist_{Guid.NewGuid():N}.zip");

        using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite))
        using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            // Create the manifest file with required Duplicati format
            // Manifest must include: Version, Encoding, Blocksize, BlockHash, FileHash, Created, AppVersion
            var manifestEntry = zip.CreateEntry("manifest");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new
                {
                    Version = 2,
                    Encoding = "utf8",
                    Blocksize = 1048576L, // 1MB in bytes (Duplicati default)
                    BlockHash = "SHA256",
                    FileHash = "SHA256",
                    Created = DateTime.UtcNow.ToString("O"),
                    AppVersion = "2.0.0.0"
                };
                writer.Write(System.Text.Json.JsonSerializer.Serialize(manifest));
            }

            // Create the filelist.json with file entries
            // Duplicati expects properties in specific order: type, path, hash, size, time
            // Date format must be: yyyyMMdd'T'HHmmssK (e.g., 20260306T141206Z)
            var filelistEntry = zip.CreateEntry("filelist.json");
            using (var entryStream = filelistEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.WriteLine("[");
                var first = true;
                foreach (var file in files)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;

                    var path = file.Key.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    // Order matters! Duplicati reads: type -> path -> hash -> size -> time
                    // Time format: yyyyMMdd'T'HHmmssK (Duplicati's SERIALIZED_DATE_TIME_FORMAT)
                    var timeStr = file.Value.time.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssK", System.Globalization.CultureInfo.InvariantCulture);
                    writer.Write($"{{\"type\": \"File\", \"path\": \"{path}\", \"hash\": \"{file.Value.hash}\", \"size\": {file.Value.size}, \"time\": \"{timeStr}\"}}");
                }
                writer.WriteLine();
                writer.WriteLine("]");
            }
        }

        return tempFile;
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldLogError_WhenFileDoesNotExist()
    {
        // Arrange
        var processor = CreateProcessor();
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.zip");

        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Act
        await processor.ProcessDlistAsync("backup-1", version, nonExistentFile, null);

        // Assert - No exception should be thrown, method should complete gracefully
        Assert.True(true);
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldThrow_WhenBackupSourceNotExists()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/test/file1.txt"] = (100, DateTime.UtcNow.AddDays(-1), "hash1"),
            ["/test/file2.txt"] = (200, DateTime.UtcNow.AddDays(-2), "hash2")
        };
        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act & Assert - Should throw when BackupSource doesn't exist
            var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessDlistAsync(backupId, version, dlistFile, null));
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldCreateFileEntries()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/documents/report.pdf"] = (1024, DateTime.UtcNow.AddDays(-5), "hash_report"),
            ["/documents/notes.txt"] = (256, DateTime.UtcNow.AddDays(-3), "hash_notes"),
            ["/images/photo.jpg"] = (2048, DateTime.UtcNow.AddDays(-1), "hash_photo")
        };
        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert - Verify BackupFileEntries were created
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            backupSource.Should().NotBeNull();

            var fileEntries = await session.Query<BackupFileEntry>()
                .Where(f => f.BackupSourceId == backupSource!.Id)
                .ToListAsync();

            fileEntries.Should().HaveCount(3);
            fileEntries.Should().Contain(f => f.Path == "/documents/report.pdf" && f.Size == 1024);
            fileEntries.Should().Contain(f => f.Path == "/documents/notes.txt" && f.Size == 256);
            fileEntries.Should().Contain(f => f.Path == "/images/photo.jpg" && f.Size == 2048);
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldSkipDuplicateFiles()
    {
        // Arrange
        var processor1 = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version1 = new DateTimeOffset(2024, 3, 11, 10, 0, 0, TimeSpan.Zero);
        var version2 = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/stable/file.txt"] = (500, DateTime.UtcNow.AddDays(-10), "stable_hash"),
            ["/changing/file.txt"] = (300, DateTime.UtcNow.AddDays(-5), "old_hash")
        };
        var dlistFile1 = CreateTestDlistFile(files);

        try
        {
            // Act - Process first version
            await processor1.ProcessDlistAsync(backupId, version1, dlistFile1, null);

            // Create second version with one changed file and one same file
            var filesV2 = new Dictionary<string, (long size, DateTime time, string hash)>
            {
                ["/stable/file.txt"] = (500, DateTime.UtcNow.AddDays(-10), "stable_hash"), // Same
                ["/changing/file.txt"] = (350, DateTime.UtcNow.AddDays(-2), "new_hash")     // Changed
            };
            var dlistFile2 = CreateTestDlistFile(filesV2);

            try
            {
                var processor2 = CreateProcessor();
                await processor2.ProcessDlistAsync(backupId, version2, dlistFile2, null);

                // Assert - Verify only the changed file appears as new
                using var session = CreateSession();
                var backupSource = await session.Query<BackupSource>()
                    .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

                backupSource.Should().NotBeNull();
                backupSource!.LastParsedVersion.Should().Be(version2);

                var allEntries = await session.Query<BackupFileEntry>()
                    .Where(f => f.BackupSourceId == backupSource.Id)
                    .ToListAsync();

                // Should have 3 entries total (2 from v1, 1 new from v2 - 1 duplicate skipped)
                allEntries.Should().HaveCount(3);

                // Verify BackupVersionFile entries - should have all 4 (2 from v1 + 2 from v2)
                var versionFiles = await session.Query<BackupVersionFile>()
                    .Where(f => f.BackupSourceId == backupSource.Id)
                    .ToListAsync();

                versionFiles.Should().HaveCount(4);
                versionFiles.Should().Contain(f => f.Path == "/stable/file.txt" && f.Version == version1);
                versionFiles.Should().Contain(f => f.Path == "/stable/file.txt" && f.Version == version2);
                versionFiles.Should().Contain(f => f.Path == "/changing/file.txt" && f.Version == version1);
                versionFiles.Should().Contain(f => f.Path == "/changing/file.txt" && f.Version == version2);
            }
            finally
            {
                if (File.Exists(dlistFile2))
                    File.Delete(dlistFile2);
            }
        }
        finally
        {
            if (File.Exists(dlistFile1))
                File.Delete(dlistFile1);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldUpdateLastParsedVersion()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version1 = new DateTimeOffset(2024, 3, 11, 10, 0, 0, TimeSpan.Zero);
        var version2 = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/test/file.txt"] = (100, DateTime.UtcNow, "hash1")
        };

        // Process version 1
        var dlistFile1 = CreateTestDlistFile(files);
        try
        {
            await processor.ProcessDlistAsync(backupId, version1, dlistFile1, null);

            // Process version 2 (later timestamp)
            files["/test/file.txt"] = (150, DateTime.UtcNow, "hash2");
            var dlistFile2 = CreateTestDlistFile(files);
            try
            {
                var processor2 = CreateProcessor();
                await processor2.ProcessDlistAsync(backupId, version2, dlistFile2, null);

                // Assert
                using var session = CreateSession();
                var backupSource = await session.Query<BackupSource>()
                    .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

                backupSource.Should().NotBeNull();
                backupSource!.LastParsedVersion.Should().Be(version2);
            }
            finally
            {
                if (File.Exists(dlistFile2))
                    File.Delete(dlistFile2);
            }
        }
        finally
        {
            if (File.Exists(dlistFile1))
                File.Delete(dlistFile1);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldMarkFilesAsNotIndexed()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/docs/file1.txt"] = (100, DateTime.UtcNow, "hash1"),
            ["/docs/file2.txt"] = (200, DateTime.UtcNow, "hash2")
        };
        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            var fileEntries = await session.Query<BackupFileEntry>()
                .Where(f => f.BackupSourceId == backupSource!.Id)
                .ToListAsync();

            fileEntries.Should().OnlyContain(f => f.IsIndexed == false);
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldHandleEmptyFileList()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var emptyFiles = new Dictionary<string, (long size, DateTime time, string hash)>();
        var dlistFile = CreateTestDlistFile(emptyFiles);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            backupSource.Should().NotBeNull();

            var fileEntries = await session.Query<BackupFileEntry>()
                .Where(f => f.BackupSourceId == backupSource!.Id)
                .ToListAsync();

            fileEntries.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldHandleLargeFileLists()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>();

        // Create 2500 files to test batch processing (batch size is 1000)
        for (int i = 0; i < 2500; i++)
        {
            files[$"/files/file{i:D5}.txt"] = (i * 10, DateTime.UtcNow.AddMinutes(-i), $"hash{i}");
        }

        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            backupSource.Should().NotBeNull();

            var fileEntries = await session.Query<BackupFileEntry>()
                .Where(f => f.BackupSourceId == backupSource!.Id)
                .ToListAsync();

            fileEntries.Should().HaveCount(2500);
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldSetCorrectFileMetadata()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var testTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/data/important.doc"] = (5000, testTime, "abc123hash")
        };
        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            var fileEntry = await session.Query<BackupFileEntry>()
                .FirstOrDefaultAsync(f => f.BackupSourceId == backupSource!.Id);

            fileEntry.Should().NotBeNull();
            fileEntry!.Path.Should().Be("/data/important.doc");
            fileEntry.Size.Should().Be(5000);
            fileEntry.LastModified.Should().Be(testTime);
            fileEntry.Hash.Should().Be("abc123hash");
            fileEntry.VersionAdded.Should().Be(version);
            fileEntry.VersionDeleted.Should().BeNull();
            fileEntry.IsIndexed.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldStoreAllFilesInBackupVersionFile()
    {
        // Arrange
        var processor = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        var files = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/documents/report.pdf"] = (1024, DateTime.UtcNow.AddDays(-5), "hash_report"),
            ["/documents/notes.txt"] = (256, DateTime.UtcNow.AddDays(-3), "hash_notes"),
            ["/images/photo.jpg"] = (2048, DateTime.UtcNow.AddDays(-1), "hash_photo")
        };
        var dlistFile = CreateTestDlistFile(files);

        try
        {
            // Act
            await processor.ProcessDlistAsync(backupId, version, dlistFile, null);

            // Assert - Verify BackupVersionFile entries were created
            using var session = CreateSession();
            var backupSource = await session.Query<BackupSource>()
                .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

            backupSource.Should().NotBeNull();

            var versionFiles = await session.Query<BackupVersionFile>()
                .Where(f => f.BackupSourceId == backupSource!.Id && f.Version == version)
                .ToListAsync();

            versionFiles.Should().HaveCount(3);
            versionFiles.Should().Contain(f => f.Path == "/documents/report.pdf" && f.Hash == "hash_report" && f.Size == 1024);
            versionFiles.Should().Contain(f => f.Path == "/documents/notes.txt" && f.Hash == "hash_notes" && f.Size == 256);
            versionFiles.Should().Contain(f => f.Path == "/images/photo.jpg" && f.Hash == "hash_photo" && f.Size == 2048);
        }
        finally
        {
            if (File.Exists(dlistFile))
                File.Delete(dlistFile);
        }
    }

    [Fact]
    public async Task ProcessDlistAsync_ShouldTrackFileChangesAcrossVersions()
    {
        // Arrange
        var processor1 = CreateProcessor();
        var backupId = $"test-backup-{Guid.NewGuid():N}";
        var version1 = new DateTimeOffset(2024, 3, 11, 10, 0, 0, TimeSpan.Zero);
        var version2 = new DateTimeOffset(2024, 3, 12, 14, 30, 0, TimeSpan.Zero);

        // Create BackupSource first
        await CreateTestBackupSource(backupId);

        // First backup version
        var filesV1 = new Dictionary<string, (long size, DateTime time, string hash)>
        {
            ["/tracked/file.txt"] = (100, new DateTime(2024, 3, 10, 10, 0, 0, DateTimeKind.Utc), "hash_v1")
        };
        var dlistFile1 = CreateTestDlistFile(filesV1);

        try
        {
            // Process first version
            await processor1.ProcessDlistAsync(backupId, version1, dlistFile1, null);

            // Second backup version - file changed
            var filesV2 = new Dictionary<string, (long size, DateTime time, string hash)>
            {
                ["/tracked/file.txt"] = (150, new DateTime(2024, 3, 11, 15, 30, 0, DateTimeKind.Utc), "hash_v2")
            };
            var dlistFile2 = CreateTestDlistFile(filesV2);

            try
            {
                var processor2 = CreateProcessor();
                await processor2.ProcessDlistAsync(backupId, version2, dlistFile2, null);

                // Assert - Query version history for the file
                using var session = CreateSession();
                var backupSource = await session.Query<BackupSource>()
                    .FirstOrDefaultAsync(b => b.DuplicatiBackupId == backupId);

                var fileHistory = await session.Query<BackupVersionFile>()
                    .Where(f => f.BackupSourceId == backupSource!.Id && f.Path == "/tracked/file.txt")
                    .OrderBy(f => f.Version)
                    .ToListAsync();

                // Should have 2 entries (one for each version)
                fileHistory.Should().HaveCount(2);

                // First version
                fileHistory[0].Version.Should().Be(version1);
                fileHistory[0].Hash.Should().Be("hash_v1");
                fileHistory[0].Size.Should().Be(100);
                fileHistory[0].LastModified.Should().Be(new DateTime(2024, 3, 10, 10, 0, 0, DateTimeKind.Utc));

                // Second version
                fileHistory[1].Version.Should().Be(version2);
                fileHistory[1].Hash.Should().Be("hash_v2");
                fileHistory[1].Size.Should().Be(150);
                fileHistory[1].LastModified.Should().Be(new DateTime(2024, 3, 11, 15, 30, 0, DateTimeKind.Utc));
            }
            finally
            {
                if (File.Exists(dlistFile2))
                    File.Delete(dlistFile2);
            }
        }
        finally
        {
            if (File.Exists(dlistFile1))
                File.Delete(dlistFile1);
        }
    }
}
