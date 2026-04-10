using DuplicatiIndexer.Data.Entities;
using Marten;
using JasperFx;

namespace DuplicatiIndexer.Data;

/// <summary>
/// Configuration for Marten document store.
/// </summary>
public static class MartenConfiguration
{
    /// <summary>
    /// Configures Marten document store options.
    /// </summary>
    /// <param name="options">The store options.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public static void Configure(StoreOptions options, string connectionString)
    {
        options.Connection(connectionString);

        // Auto-create schema objects (tables, indexes, etc.)
        options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

        // Configure BackupSource document
        options.Schema.For<BackupSource>()
            .Identity(x => x.Id)
            .Index(x => x.DuplicatiBackupId)
            .UniqueIndex(x => x.DuplicatiBackupId);

        // Configure BackupFileEntry document
        options.Schema.For<BackupFileEntry>()
            .Identity(x => x.Id)
            .Index(x => x.BackupSourceId)
            .Index(x => x.Path)
            .Index(x => x.Hash)
            .Index(x => x.VersionAdded)
            .Index(x => new { x.VersionAdded, x.VersionDeleted });

        // Configure BackupVersionFile document - tracks all files in each backup version
        options.Schema.For<BackupVersionFile>()
            .Identity(x => x.Id)
            .Index(x => x.BackupSourceId)
            .Index(x => x.Version)
            .Index(x => x.Path)
            .Index(x => new { x.BackupSourceId, x.Version })
            .Index(x => new { x.BackupSourceId, x.Path })
            .Index(x => new { x.BackupSourceId, x.Path, x.Version });

        // Configure IndexingJob document
        options.Schema.For<IndexingJob>()
            .Identity(x => x.Id)
            .Index(x => x.BackupFileEntryId)
            .Index(x => x.Status)
            .Index(x => x.CreatedAt);

        // Configure QuerySession document
        options.Schema.For<QuerySession>()
            .Identity(x => x.Id)
            .Index(x => x.CreatedAt)
            .Index(x => x.LastActivityAt);

        // Configure QueryHistoryItem document
        options.Schema.For<QueryHistoryItem>()
            .Identity(x => x.Id)
            .Index(x => x.SessionId)
            .Index(x => x.QueryTimestamp);

        // Configure IndexedContent document for full-text search
        options.Schema.For<IndexedContent>()
            .Identity(x => x.Id)
            .Index(x => x.FileEntryId)
            .Index(x => x.ChunkIndex)
            .GinIndexJsonData(); // Enable GIN index on JSONB for faster queries
    }
}
