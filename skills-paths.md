# DuplicatiIndexer Path Search Skill

This skill enables OpenClaw to search for files by path pattern in the indexed backup metadata.

## Overview

The path search endpoint allows searching through all indexed file paths using wildcard patterns. This searches file metadata (not content) and is useful for locating specific files by name or path pattern across all backup versions.

## Endpoint

**URL:** `POST /api/search/paths`

**Base URL:** Configured per deployment (e.g., `http://localhost:8080`)

## Authentication

No authentication required (local/secured network deployment).

## Request Format

### Content-Type

`application/json`

### Request Body

| Field            | Type    | Required | Default | Description                                              |
| ---------------- | ------- | -------- | ------- | -------------------------------------------------------- |
| `pattern`        | string  | Yes      | -       | Path pattern to search for (supports wildcards \* and ?) |
| `backupSourceId` | guid    | No       | null    | Filter to a specific backup source                       |
| `limit`          | integer | No       | 50      | Maximum number of results to return (1-200)              |

### Wildcard Patterns

- `*` matches any sequence of characters
- `?` matches any single character

### Example Requests

**Basic search:**

```json
{
  "pattern": "*.pdf"
}
```

**Search in specific directory:**

```json
{
  "pattern": "/backups/reports/*2024*.xlsx",
  "limit": 20
}
```

**Search specific backup source:**

```json
{
  "pattern": "*.sql",
  "backupSourceId": "550e8400-e29b-41d4-a716-446655440000",
  "limit": 100
}
```

## Response Format

### Success Response (200 OK)

```json
{
  "pattern": "*.pdf",
  "totalCount": 25,
  "results": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "path": "/backups/documents/report_2024.pdf",
      "hash": "a1b2c3d4e5f6...",
      "size": 1543200,
      "lastModified": "2024-03-15T10:30:00Z",
      "backupSourceId": "550e8400-e29b-41d4-a716-446655440000",
      "backupSourceName": "Production Backups",
      "isIndexed": true,
      "versionAdded": "2024-03-15T12:00:00Z"
    }
  ]
}
```

### Response Fields

| Field                        | Type     | Description                                |
| ---------------------------- | -------- | ------------------------------------------ |
| `pattern`                    | string   | The search pattern used                    |
| `totalCount`                 | integer  | Number of results returned                 |
| `results`                    | array    | List of matching files                     |
| `results[].id`               | guid     | Unique file identifier                     |
| `results[].path`             | string   | Full file path                             |
| `results[].hash`             | string   | File hash for deduplication                |
| `results[].size`             | integer  | File size in bytes                         |
| `results[].lastModified`     | datetime | Original file modification time            |
| `results[].backupSourceId`   | guid     | ID of the backup source                    |
| `results[].backupSourceName` | string   | Name of the backup source                  |
| `results[].isIndexed`        | boolean  | Whether file content is indexed for search |
| `results[].versionAdded`     | datetime | When this file version was added to backup |

### Error Responses

- **400 Bad Request**: Invalid pattern or parameters
  ```json
  { "error": "Pattern is required" }
  ```
  ```json
  { "error": "Limit must be between 1 and 200" }
  ```

## Usage Examples

### Find all PDF files

```bash
curl -X POST http://localhost:8080/api/search/paths \
  -H "Content-Type: application/json" \
  -d '{"pattern": "*.pdf"}'
```

### Find database backup files

```bash
curl -X POST http://localhost:8080/api/search/paths \
  -H "Content-Type: application/json" \
  -d '{"pattern": "*backup*.sql", "limit": 20}'
```

### Find files in a specific folder

```bash
curl -X POST http://localhost:8080/api/search/paths \
  -H "Content-Type: application/json" \
  -d '{"pattern": "/backups/2024/*", "limit": 100}'
```

## When to Use

Use this endpoint when you need to:

- Locate files by name or path pattern
- Find all files of a specific type (e.g., _.pdf, _.docx)
- Browse files in a specific directory structure
- Check if a file exists in backups
- Verify indexing status of files before content search

## Differences from RRF Search

| Feature      | Path Search           | RRF Search                    |
| ------------ | --------------------- | ----------------------------- |
| Searches     | File metadata (paths) | File content                  |
| Pattern type | Wildcard (\*, ?)      | Natural language              |
| Results      | Exact path matches    | Semantic relevance            |
| Use case     | Finding files         | Finding content               |
| Speed        | Very fast             | Slower (vector + text search) |

## Integration Notes

1. Results only include files that exist in the most recent backup version (not deleted)
2. Use the `isIndexed` field to check if a file's content is available for RRF search
3. Combine with `/api/files/history` to get version history for a specific file
4. The `hash` field can be used to identify duplicate files across paths
