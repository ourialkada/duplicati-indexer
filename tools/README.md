# DuplicatiIndexer Tools

This directory contains utility tools for interacting with the DuplicatiIndexer service.

---

# Dlist Message Injector Tool

A Python command-line tool to inject `BackupVersionCreated` messages into Wolverine PostgreSQL persistence for the DuplicatiIndexer service.

## Overview

This tool inserts messages directly into the PostgreSQL table used by Wolverine (the message bus), which triggers dlist file processing in the DuplicatiIndexer service. The message format matches the [`BackupVersionCreated`](Indexer/Messages/BackupVersionCreated.cs:1) record structure used by the handler at [`BackupVersionCreatedHandler`](Indexer/Handlers/BackupVersionCreatedHandler.cs:1).

Wolverine uses a simpler message envelope format than the previous MassTransit implementation. Messages are stored in the `wolverine.incoming_envelopes` table with the following columns:
- `id` - Message UUID
- `owner_id` - 0 for local processing
- `destination` - "local" for local handlers
- `body` - JSON payload containing the message data
- `message_type` - "DuplicatiIndexer.Messages.BackupVersionCreated"
- `attempts` - Initial retry count (0)
- `scheduled_time` - NULL (immediate processing)
- `deliver_by` - NULL (no expiration)

## Prerequisites

- Python 3.7+
- PostgreSQL server running (see [docker-compose.yml](../docker-compose.yml) for service configuration)
- `psycopg2-binary` library installed

## Installation

1. Install the required Python dependency:

```bash
pip install psycopg2-binary
```

## Usage

### Basic Usage

```bash
python inject_dlist_message.py \
    --backup-id "550e8400-e29b-41d4-a716-446655440000" \
    --version 1 \
    --dlist-url "/path/to/backup.dlist"
```

### With Passphrase (for encrypted backups)

```bash
python inject_dlist_message.py \
    --backup-id "my-backup-123" \
    --version 5 \
    --dlist-url "/backups/data.dlist" \
    --passphrase "my-secret-passphrase"
```

### With Custom PostgreSQL Connection

```bash
python inject_dlist_message.py \
    --host "postgres.example.com" \
    --port 5432 \
    --database "duplicati_indexer" \
    --username "admin" \
    --password "secret" \
    --backup-id "abc-123" \
    --version 2 \
    --dlist-url "/data/file.dlist"
```

## Command-Line Arguments

### Message Parameters (Required)

| Argument | Description | Type |
|----------|-------------|------|
| `--backup-id` | The backup identifier (GUID or string) | string |
| `--version` | The version number (positive integer) | int |
| `--dlist-url` | Path to the dlist file | string |

### Message Parameters (Optional)

| Argument | Description | Default |
|----------|-------------|---------|
| `--passphrase` | Encryption passphrase for encrypted backups | None |

### PostgreSQL Connection Parameters (Optional)

| Argument | Description | Default |
|----------|-------------|---------|
| `--host` | PostgreSQL server hostname | `localhost` |
| `--port` | PostgreSQL server port | `5432` |
| `--database` | PostgreSQL database name | `duplicati_indexer` |
| `--username` | PostgreSQL username | `postgres` |
| `--password` | PostgreSQL password | `postgres` |
| `--queue` | **Legacy** - kept for backward compatibility, ignored | `BackupVersionCreated` |

## Environment Variables

The tool supports configuration via environment variables (in order of priority):

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__MessageStore` | Full connection string for Wolverine messaging (highest priority) |
| `POSTGRES_HOST` | PostgreSQL server hostname |
| `POSTGRES_PORT` | PostgreSQL server port |
| `POSTGRES_DB` | PostgreSQL database name |
| `POSTGRES_USER` | PostgreSQL username |
| `POSTGRES_PASSWORD` | PostgreSQL password |

Example using connection string:

```bash
export ConnectionStrings__MessageStore="Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=secret"
python inject_dlist_message.py --backup-id "abc-123" --version 2 --dlist-url "/data/file.dlist"
```

## Message Format

The tool inserts messages into the `wolverine.incoming_envelopes` table with the following JSON body:

```json
{
  "BackupId": "550e8400-e29b-41d4-a716-446655440000",
  "Version": 1,
  "DlistFilePath": "/path/to/backup.dlist",
  "Passphrase": null
}
```

The `message_type` column is set to `DuplicatiIndexer.Messages.BackupVersionCreated`.

## Examples

### Inject a message for a new backup version

```bash
python inject_dlist_message.py \
    --backup-id "backup-001" \
    --version 1 \
    --dlist-url "/data/backups/backup-001/1.dlist"
```

### Inject with all options

```bash
python inject_dlist_message.py \
    --host "localhost" \
    --port 5432 \
    --database "duplicati_indexer" \
    --username "postgres" \
    --password "postgres" \
    --backup-id "org-123_backup-456" \
    --version 42 \
    --dlist-url "/mnt/backups/backup-456/dlist/42.dlist" \
    --passphrase "my-encryption-key"
```

## Error Handling

The tool provides clear error messages for common issues:

- **Connection errors**: Displays PostgreSQL host, port, and database being used
- **Invalid version**: Validates version is a positive integer
- **Missing required parameters**: Shows help and validation errors
- **Table does not exist**: Reports if Wolverine hasn't been initialized
- **Authentication failures**: Reports invalid credentials

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - message inserted |
| 1 | Error - connection failed, validation error, or insert failed |

## Integration with DuplicatiIndexer

When this tool inserts a message:

1. Message is stored in the `wolverine.incoming_envelopes` table
2. The [`BackupVersionCreatedHandler`](Indexer/Handlers/BackupVersionCreatedHandler.cs:1) receives the message
3. The handler calls [`DlistProcessor.ProcessDlistAsync()`](Indexer/Services/DlistProcessor.cs:1) to process the dlist file
4. The dlist file is parsed and indexed for search

## Migration from MassTransit

If you were using the previous MassTransit version of this tool:

- **Database**: Changed from `masstransit` to `duplicati_indexer` (shared with Marten)
- **Table**: Changed from `transport.queue_backupversioncreated` to `wolverine.incoming_envelopes`
- **Envelope format**: Simplified - Wolverine uses a much simpler format than MassTransit
- **Queue argument**: The `--queue` argument is now legacy/ignored
- **Message type**: Changed format from `urn:message:DuplicatiIndexer.Messages:BackupVersionCreated` to `DuplicatiIndexer.Messages.BackupVersionCreated`

## Troubleshooting

### Connection Refused

Ensure PostgreSQL is running:

```bash
docker ps  # Check if PostgreSQL container is running
```

Or with the docker-compose setup:

```bash
docker-compose up -d
```

### Authentication Failed

Default credentials are `postgres`/`postgres`. If PostgreSQL was configured with different credentials, use `--username` and `--password` arguments.

### Table Does Not Exist

The tool expects the `wolverine.incoming_envelopes` table to exist (created by Wolverine). If the table doesn't exist:

1. Ensure the DuplicatiIndexer service has started at least once (Wolverine auto-creates tables)
2. Check that Wolverine is properly configured in the application

### Message Not Processed

- Verify the DuplicatiIndexer service is running
- Check the dlist file path is accessible to the DuplicatiIndexer service
- Review the handler logs for processing errors

---

# Backup Source Injector Tool

A Python command-line tool to inject `BackupSource` records directly into PostgreSQL for the DuplicatiIndexer service.

## Overview

This tool inserts BackupSource documents directly into the PostgreSQL database using Marten's document format. The [`BackupSource`](Indexer/Data/Entities/BackupSource.cs:1) entity represents a backup source configuration in the system.

Note: Both Marten (document store) and Wolverine (message bus) share the same PostgreSQL database, but use different tables:
- Marten: `mt_doc_*` tables for documents
- Wolverine: `wolverine.*` tables for messages

## Prerequisites

- Python 3.7+
- PostgreSQL server running (see [docker-compose.yml](../docker-compose.yml) for service configuration)
- `psycopg2-binary` library installed

## Installation

1. Install the required Python dependencies:

```bash
pip install psycopg2-binary
```

## Usage

### Basic Usage

```bash
python inject_backup_source.py \
    --name "My Backup" \
    --duplicati-backup-id "backup-123"
```

### With Specific ID and Timestamp

```bash
python inject_backup_source.py \
    --name "Production Backup" \
    --duplicati-backup-id "prod-backup-001" \
    --source-id "550e8400-e29b-41d4-a716-446655440000" \
    --created-at "2024-01-15T10:30:00Z"
```

### With Custom PostgreSQL Connection

```bash
python inject_backup_source.py \
    --host "db.example.com" \
    --port 5432 \
    --database "mydb" \
    --username "admin" \
    --password "secret" \
    --name "Test Backup" \
    --duplicati-backup-id "test-123"
```

## Command-Line Arguments

### BackupSource Parameters (Required)

| Argument | Description | Type |
|----------|-------------|------|
| `--name` | The display name of the backup source | string |
| `--duplicati-backup-id` | The Duplicati backup identifier | string |

### BackupSource Parameters (Optional)

| Argument | Description | Default |
|----------|-------------|---------|
| `--source-id` | GUID for the backup source | Auto-generated UUID |
| `--created-at` | Creation timestamp (ISO 8601 format) | Current time |
| `--last-parsed-version` | Last parsed backup version | 0 |

### PostgreSQL Connection Parameters (Optional)

| Argument | Description | Default |
|----------|-------------|---------|
| `--host` | PostgreSQL server hostname | `localhost` |
| `--port` | PostgreSQL server port | `5432` |
| `--database` | PostgreSQL database name | `duplicati_indexer` |
| `--username` | PostgreSQL username | `postgres` |
| `--password` | PostgreSQL password | `postgres` |

## Environment Variables

The tool supports configuration via environment variables (in order of priority):

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DocumentStore` | Full connection string for Marten documents (highest priority) |
| `POSTGRES_HOST` | PostgreSQL server hostname |
| `POSTGRES_PORT` | PostgreSQL server port |
| `POSTGRES_DB` | PostgreSQL database name |
| `POSTGRES_USER` | PostgreSQL username |
| `POSTGRES_PASSWORD` | PostgreSQL password |

## Document Format

The tool inserts documents into the `mt_doc_backupsource` table using Marten's JSONB format:

```json
{
  "Id": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "My Backup",
  "DuplicatiBackupId": "backup-123",
  "CreatedAt": "2024-01-15T10:30:00+00:00",
  "LastParsedVersion": 0
}
```

## Error Handling

The tool provides clear error messages for common issues:

- **Connection errors**: Displays PostgreSQL host, port, and database being used
- **Invalid UUID format**: Validates the source-id format if provided
- **Invalid timestamp**: Validates ISO 8601 format for created-at
- **Database errors**: Shows PostgreSQL error details

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - document inserted |
| 1 | Error - connection failed, validation error, or insert failed |

## Integration with DuplicatiIndexer

When this tool inserts a BackupSource:

1. Document is stored in the `mt_doc_backupsource` table (Marten document store)
2. The [`DlistProcessor`](Indexer/Services/DlistProcessor.cs:1) uses the BackupSource to track parsing progress
3. The [`BackupVersionCreatedHandler`](Indexer/Handlers/BackupVersionCreatedHandler.cs:1) references the `DuplicatiBackupId` to associate processed files with the source
4. The `LastParsedVersion` field tracks which backup versions have been processed

## Troubleshooting

### Connection Refused

Ensure PostgreSQL is running:

```bash
docker ps  # Check if PostgreSQL container is running
```

Or with the docker-compose setup:

```bash
docker-compose up -d
```

### Authentication Failed

Default credentials are `postgres`/`postgres`. If PostgreSQL was configured with different credentials, use `--username` and `--password` arguments.

### Table Does Not Exist

The tool expects the `mt_doc_backupsource` table to exist (created by Marten). If the table doesn't exist:

1. Ensure the DuplicatiIndexer service has started at least once (Marten auto-creates tables)
2. Or manually create the table with proper Marten schema

### Duplicate Key Error

If you specify a `--source-id` that already exists, the tool will update the existing record (upsert behavior).

---

## Common Workflows

### Setting up a new backup source and triggering initial processing

```bash
# 1. Create the backup source
python inject_backup_source.py \
    --name "My Production Backup" \
    --duplicati-backup-id "prod-backup-001"

# 2. Inject a message to process the first version
python inject_dlist_message.py \
    --backup-id "prod-backup-001" \
    --version 1 \
    --dlist-url "/backups/prod-backup-001/1.dlist"
```

### Processing an encrypted backup

```bash
python inject_dlist_message.py \
    --backup-id "encrypted-backup-001" \
    --version 5 \
    --dlist-url "/backups/encrypted/5.dlist" \
    --passphrase "my-secret-encryption-key"
```

### Using environment variables for all connections

```bash
# Set up environment variables
export ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=secret"
export ConnectionStrings__MessageStore="Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=secret"

# Create backup source
python inject_backup_source.py \
    --name "Test Backup" \
    --duplicati-backup-id "test-123"

# Inject processing message
python inject_dlist_message.py \
    --backup-id "test-123" \
    --version 1 \
    --dlist-url "/data/test-123/1.dlist"
```

Note: Both tools can use the same connection string since Marten and Wolverine share the same PostgreSQL database.
