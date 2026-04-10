# DuplicatiIndexer RRF Search Skill

This skill enables OpenClaw to perform Reciprocal Rank Fusion (RRF) hybrid searches against the DuplicatiIndexer API.

## Overview

The RRF search endpoint combines dense vector search (semantic similarity) with sparse full-text search (keyword matching) using Reciprocal Rank Fusion to provide highly relevant search results across indexed backup content.

## Endpoint

**URL:** `POST /api/search/rrf`

**Base URL:** Configured per deployment (e.g., `http://localhost:8080`)

## Authentication

No authentication required (local/secured network deployment).

## Request Format

### Content-Type

`application/json`

### Request Body

| Field               | Type    | Required | Default | Description                                                               |
| ------------------- | ------- | -------- | ------- | ------------------------------------------------------------------------- |
| `query`             | string  | Yes      | -       | The search query text                                                     |
| `topKPerMethod`     | integer | No       | 10      | Number of results to retrieve from each search method (vector and sparse) |
| `finalTopK`         | integer | No       | 5       | Final number of results to return after RRF fusion                        |
| `rrfK`              | integer | No       | 60      | RRF constant k. Higher values reduce the impact of rank differences       |
| `vectorWeight`      | number  | No       | 1.0     | Weight for vector search results (when using weighted fusion)             |
| `sparseWeight`      | number  | No       | 1.0     | Weight for sparse search results (when using weighted fusion)             |
| `useWeightedFusion` | boolean | No       | false   | Use weighted fusion instead of standard RRF                               |

### Example Request

```json
{
  "query": "quarterly financial reports 2024",
  "topKPerMethod": 15,
  "finalTopK": 10,
  "rrfK": 60,
  "vectorWeight": 1.2,
  "sparseWeight": 0.8,
  "useWeightedFusion": true
}
```

## Response Format

### Success Response (200 OK)

```json
{
  "query": "quarterly financial reports 2024",
  "totalCount": 10,
  "results": [
    {
      "id": "doc-uuid-123",
      "content": "Q1 2024 Financial Report shows revenue growth of...",
      "score": 0.0234,
      "rank": 1,
      "source": "hybrid",
      "metadata": {
        "rrf_score": 0.0234,
        "sources": ["vector", "sparse"],
        "file_path": "/backups/reports/q1_2024.pdf",
        "chunk_index": 3
      }
    }
  ]
}
```

### Response Fields

| Field                | Type    | Description                                            |
| -------------------- | ------- | ------------------------------------------------------ |
| `query`              | string  | The original search query                              |
| `totalCount`         | integer | Number of results returned                             |
| `results`            | array   | List of search results                                 |
| `results[].id`       | string  | Unique document/chunk identifier                       |
| `results[].content`  | string  | The text content of the result                         |
| `results[].score`    | number  | RRF fusion score (higher is better)                    |
| `results[].rank`     | integer | Rank position (1-based)                                |
| `results[].source`   | string  | Source type ("hybrid" or "hybrid_weighted")            |
| `results[].metadata` | object  | Additional metadata including file_path, sources, etc. |

### Error Responses

- **400 Bad Request**: Invalid query or parameters
  ```json
  { "error": "Query is required" }
  ```

## Usage Examples

### Basic Search

```bash
curl -X POST http://localhost:8080/api/search/rrf \
  -H "Content-Type: application/json" \
  -d '{"query": "password reset procedures"}'
```

### Advanced Search with Weights

```bash
curl -X POST http://localhost:8080/api/search/rrf \
  -H "Content-Type: application/json" \
  -d '{
    "query": "database migration scripts",
    "topKPerMethod": 20,
    "finalTopK": 15,
    "useWeightedFusion": true,
    "vectorWeight": 1.5,
    "sparseWeight": 0.5
  }'
```

## When to Use

Use this endpoint when you need to:

- Search across all indexed backup content
- Find documents by semantic meaning (not just exact keywords)
- Combine keyword and semantic search for best results
- Retrieve relevant content chunks for RAG applications

## Integration Notes

1. The endpoint performs both vector similarity search and full-text search in parallel
2. Results are fused using Reciprocal Rank Fusion (RRF) formula: `score = 1 / (k + rank)`
3. The `sources` metadata field indicates which search methods found each result
4. Results are ranked by RRF score in descending order
5. The content field contains the actual text chunk that matched the query
