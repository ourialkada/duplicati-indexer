# Duplicati Indexer

> **Find anything in your backups. Instantly.**

Stop digging through endless backup files. Duplicati Indexer transforms your static backups into a searchable knowledge base using AI-powered semantic search. Ask natural language questions like *"What was the Q4 revenue in last year's reports?"* and get precise answers in seconds—not hours.

## Why Duplicati Indexer?

- **Semantic Search**: Find documents by meaning, not just filenames
- **AI-Powered Q&A**: Chat with your backup content using RAG (Retrieval-Augmented Generation)
- **Ransomware Protection**: Built-in 4-layer detection stops encrypted garbage from polluting your index
- **Multi-LLM Support**: Use Google Gemini, OpenAI, Claude, or run everything locally with Ollama
- **Privacy First**: Local processing options keep your data on your infrastructure

**Note:** This project is a work in progress and is not yet ready for production use.
**Warning:** There is currently no encryption or authentication in this project. Do not use in production.

## Architecture

The system consists of several components:

- **Indexer Worker Service**: A .NET 10 worker service that processes Duplicati backups using Wolverine message handlers.
- **Frontend**: An Angular web UI for querying indexed backup content (available on port 3000).
- **Wolverine with PostgreSQL Persistence**: Message broker using PostgreSQL for queuing indexing jobs.
- **PostgreSQL**: Relational database with Marten document store with Wolverine integration for tracking backup sources, file versions, and indexing status.
- **Qdrant**: Vector database for storing and searching document embeddings.
- **Unstructured.io**: API service for extracting text from various document formats (PDF, Word, etc.).
- **Ollama**: Local embedding model server (nomic-embed-text) for generating text embeddings.
- **Ollama LLM** (optional): Local LLM server for running open-source models like Llama, Gemma, Phi, etc.
- **LLM Services**: Supports multiple LLM providers for generating embeddings and answering queries:
  - Google Gemini (default)
  - OpenAI ChatGPT
  - Anthropic Claude
  - Local models via Ollama (llama3.2, gemma3, phi-4-mini, qwen3, etc.)
  - Local models via LMStudio

## Ransomware Protection

The system features a built-in, completely local **Ransomware Detection Module**. To prevent indexing or syncing of encrypted "garbage" data, the module intercepts files during the backup indexing process and applies a high-performance 4-layer heuristic network:

- **Canary Files**: Instantly halts the backup process if hidden tripwire files (e.g., `._canary.docx`) are maliciously modified.
- **Shannon Entropy**: Evaluates binary entropy using optimized O(N) span processing. If a normally low-entropy file type (like `.txt`) exceeds the critical threshold (~7.9), it is flagged.
- **Magic Byte Verification**: Verifies the file's header signature against its extension to defeat renaming tricks.
- **Velocity Threshold**: Monitors the frequency of anomalous files. If the limit is triggered, a global circuit breaker pauses the Backup Source.

## Prerequisites

- Docker and Docker Compose
- .NET 10 SDK (for local development)
- API keys for at least one LLM provider (if not using local models)
- Git (for cloning the repository with submodules)

## Getting Started

### Clone the Repository

This project includes the Duplicati repository as a Git submodule. To clone the repository with all submodules:

```bash
# Clone with all submodules
git clone --recursive git@github.com:<username>/DuplicatiIndexer.git

# Or if you've already cloned without submodules
git clone git@github.com:<username>/DuplicatiIndexer.git
cd DuplicatiIndexer
git submodule update --init --recursive
```

The `modules/duplicati` submodule contains the Duplicati source code and is required for building and running integration tests.

## Quick Start

### Running with Docker Compose

The easiest way to run the entire stack is using Docker Compose. Choose your LLM provider:

#### Using Google Gemini (default)

```bash
LLM_PROVIDER=Gemini GEMINI_API_KEY=<your-key> docker-compose up --build
```

#### Using OpenAI ChatGPT

```bash
LLM_PROVIDER=ChatGPT CHATGPT_API_KEY=<your-key> docker-compose up --build
```

#### Using Anthropic Claude

```bash
LLM_PROVIDER=Claude CLAUDE_API_KEY=<your-key> docker-compose up --build
```

#### Using Local Models (Ollama)

Run with a specific local model (e.g., llama3.2):

```bash
LLM_PROVIDER=local:llama3.2 docker-compose up --build
```

Or use the default Ollama model:

```bash
LLM_PROVIDER=ollama OLLAMALLM_MODEL=llama3.2 docker-compose up --build
```

#### Using LMStudio (Docker configuration)

To use a single host-running LMStudio server for both embeddings and chat completions, create a `.env` file in the root of the project with your desired configurations:

```env
LLM_PROVIDER=lmstudio
EMBED_PROVIDER=lmstudio
LMSTUDIO_BASEURL=http://host.docker.internal:1234/v1
LMSTUDIO_MODEL=mlx-community/qwen3.5-35b-a3b
LMSTUDIO_EMBEDMODEL=text-embedding-nomic-embed-text-v1.5
```

Once your `.env` file is saved, start the stack by running:

```bash
docker compose up --build
```

### Available Services

Once running, the following services are available:

- **Frontend**: http://localhost:3000 - Web UI for querying backups
- **Indexer API**: http://localhost:8080 - REST API for the indexer service
- **PostgreSQL**: port 5432 - Database for document and message storage
- **Qdrant**: ports 6333 (REST) and 6334 (gRPC) - Vector database
- **Unstructured API**: port 8000 - Document text extraction
- **Ollama**: port 11434 - Embedding model server
- **Ollama LLM**: port 11435 - Local LLM server (when using local models)

### Configuration

The Indexer service is configured via environment variables. All settings can be customized when running with Docker Compose:

| Environment Variable       | Description                                                                | Default                              |
| -------------------------- | -------------------------------------------------------------------------- | ------------------------------------ |
| `LLM_PROVIDER`             | LLM provider (Gemini, ChatGPT, Claude, ollama, lmstudio, or local:<model>) | Gemini                               |
| `EMBED_PROVIDER`           | Embedding provider (ollama, lmstudio)                                      | ollama                               |
| `GEMINI_API_KEY`           | Google Gemini API key                                                      | (empty)                              |
| `GEMINI_MODEL`             | Gemini model name                                                          | gemini-2.5-flash                     |
| `CHATGPT_API_KEY`          | OpenAI API key                                                             | (empty)                              |
| `CHATGPT_MODEL`            | ChatGPT model name                                                         | gpt-4o-mini                          |
| `LMSTUDIO_BASEURL`         | LMStudio Local Server Base URL                                             | http://localhost:1234/v1             |
| `LMSTUDIO_MODEL`           | LMStudio model name                                                        | local-model                          |
| `LMSTUDIO_EMBEDMODEL`      | LMStudio model name for text embeddings                                    | text-embedding-nomic-embed-text-v1.5 |
| `CLAUDE_API_KEY`           | Anthropic API key                                                          | (empty)                              |
| `CLAUDE_MODEL`             | Claude model name                                                          | claude-3-5-sonnet-20241022           |
| `CLAUDE_MAX_TOKENS`        | Maximum tokens for Claude responses                                        | 1024                                 |
| `OLLAMALLM_MODEL`          | Ollama model for local LLM                                                 | llama3.2                             |
| `OLLAMAEMBED_MODEL`        | Ollama model for embeddings                                                | nomic-embed-text                     |
| `POSTGRES_PASSWORD`        | PostgreSQL password                                                        | postgres                             |
| `VECTORSTORE_COLLECTION`   | Qdrant collection name                                                     | backup_content                       |
| `MAX_FILE_SIZE`            | Maximum file size to index (bytes)                                         | 104857600 (100MB)                    |
| `CHUNKING_MAX_SIZE`        | Maximum chunk size in tokens                                               | 1024                                 |
| `CHUNKING_OVERLAP`         | Chunk overlap in tokens                                                    | 50                                   |
| `CHUNKING_CHARS_PER_TOKEN` | Characters per token approximation                                         | 4.0                                  |
| `RAGQUERY_VERSION`         | RAG service version (v1 or v2)                                             | v2                                   |
| `LOG_LEVEL`                | Logging level                                                              | Information                          |
| `DOTNET_ENVIRONMENT`       | .NET environment                                                           | Production                           |

### Local Development

To run the Indexer service locally (outside of Docker):

1. Start the dependencies using Docker Compose:

   ```bash
   docker-compose up -d postgres qdrant unstructured ollama
   ```

2. Set your LLM provider and API key:

   ```bash
   export LLM_PROVIDER=Gemini
   export GEMINI_API_KEY=<your-key>
   ```

3. Run the worker service:
   ```bash
   cd Indexer
   dotnet run
   ```

For local LLM development with Ollama:

```bash
# Terminal 1: Start Ollama LLM service
export LLM_PROVIDER=local:llama3.2
docker-compose up ollama-llm

# Terminal 2: Run the indexer
cd Indexer
dotnet run
```

### Running Tests

The solution includes a suite of unit tests using xUnit, Moq, and FluentAssertions.

To run the tests:

```bash
dotnet test
```

## Test Data Setup

For integration testing and development, sample test data can be generated using the Enron Email Dataset. The [`testdata`](./testdata/) directory contains scripts and instructions for setting up a test environment that simulates weekly backups.

To set up the test data:

1. Run the `./fetch-test-data.sh` script from the project root to automatically pull and extract the dataset.
   _(Alternatively: manually download the Enron dataset from the website and extract its contents into `testdata/maildir`)_
2. Under `testdata/`, run `python get_week_data.py` to organize parsing chronological weeks.
3. Run `python backup_weeks.py` to create simulated weekly backups in `backupdest`.

This test setup allows you to verify the indexer functionality with realistic email data without using actual personal backups.

After the test data is prepared, you can run the [`create-test-backupentry.sh`](./Indexer/create-test-backupentry.sh) and [`inject-all-dlists.sh`](./Indexer/inject-all-dlists.sh) scripts to inject the test data into the system:

```bash
# Create a test backup entry in the database
./Indexer/create-test-backupentry.sh

# Inject all dlist files from the test backup
./Indexer/inject-all-dlists.sh
```

## How it Works

1. A `BackupVersionCreated` message is published to the Wolverine message store containing the backup ID, version, and path to the `dlist` file.
2. The `BackupVersionCreatedHandler` receives the message and starts processing.
3. The `DlistProcessor` reads the `dlist` file (decrypting it if necessary) and updates the PostgreSQL database via Marten with the files present in that backup version.
4. The `DiffCalculator` determines which files are new or modified since the last indexed version.
5. The `FileRestorer` extracts the actual file content from the Duplicati backup volumes (`dblock` files).
6. The `UnstructuredIndexer` sends the file content to the Unstructured API to extract text.
7. The extracted text is chunked, embedded using the configured LLM service, and stored in the Qdrant vector database by the `QdrantVectorStore`.

## RAG Query Capability

The system supports RAG (Retrieval-Augmented Generation) queries that combine vector search with LLM-generated answers:

- Use `RagQueryService` to perform semantic searches across indexed backup content
- The service retrieves relevant document chunks from Qdrant and uses the configured LLM to generate contextual answers
- Supports querying backup content using natural language questions

## OpenClaw Integration

The Indexer provides API endpoints for integration with OpenClaw, enabling AI agents to search your backups:

### RRF Hybrid Search (`POST /api/search/rrf`)

Combines dense vector search (semantic) with sparse full-text search using Reciprocal Rank Fusion:

```bash
curl -X POST http://localhost:8080/api/search/rrf \
  -H "Content-Type: application/json" \
  -d '{
    "query": "quarterly financial reports 2024",
    "topKPerMethod": 15,
    "finalTopK": 10
  }'
```

### Path Search (`POST /api/search/paths`)

Search for files by path pattern using wildcards:

```bash
curl -X POST http://localhost:8080/api/search/paths \
  -H "Content-Type: application/json" \
  -d '{
    "pattern": "*.pdf",
    "limit": 50
  }'
```

See [`skills-rrf.md`](./skills-rrf.md) and [`skills-paths.md`](./skills-paths.md) for complete API documentation.

## LLM Provider Configuration

The system supports multiple LLM providers for embeddings and RAG queries:

| Provider                    | `LLM_PROVIDER` Value | Required Environment Variables                           |
| --------------------------- | -------------------- | -------------------------------------------------------- |
| **Google Gemini** (default) | `Gemini`             | `GEMINI_API_KEY`                                         |
| **OpenAI ChatGPT**          | `ChatGPT`            | `CHATGPT_API_KEY`                                        |
| **Anthropic Claude**        | `Claude`             | `CLAUDE_API_KEY`                                         |
| **LMStudio**                | `lmstudio`           | None (uses `LMSTUDIO_BASEURL` and `LMSTUDIO_MODEL`)      |
| **Ollama (default model)**  | `ollama`             | None (uses `OLLAMALLM_MODEL` env var, default: llama3.2) |
| **Ollama (specific model)** | `local:<model>`      | None (e.g., `local:llama3.2`, `local:gemma3`)            |

### Cloud Provider Setup

For cloud-based LLM providers, obtain an API key from the respective service:

- **Gemini**: https://ai.google.dev/
- **ChatGPT**: https://platform.openai.com/
- **Claude**: https://www.anthropic.com/

### Local Model Setup (Ollama)

For local models, no API key is required. The system will automatically:

1. Start an Ollama container
2. Download the specified model
3. Use it for both embeddings (nomic-embed-text) and chat completions

Supported local models include:

- `llama3.2` (default) - Meta's Llama 3.2
- `gemma3` - Google's Gemma 3
- `phi-4-mini` - Microsoft's Phi-4 Mini
- `qwen3` - Alibaba's Qwen3
- Any other model available on Ollama Hub

Configure the desired provider using the `LLM__Provider` setting in `appsettings.json` or the `LLM_PROVIDER` environment variable.
