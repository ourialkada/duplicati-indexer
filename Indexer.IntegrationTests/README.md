# Integration Tests for DuplicatiIndexer

This project contains integration tests that use Docker containers to test the full backup and indexing workflow.

## Prerequisites

### Required Software
- Docker Desktop or Docker Engine
- .NET 10 SDK
- Duplicati CLI (optional - tests will try to find it automatically)

### Building Duplicati CLI
If Duplicati CLI is not installed, you need to build it from the submodule:

```bash
cd modules/duplicati
dotnet build Executables/Duplicati.CommandLine/Duplicati.CommandLine.csproj -c Release
```

## Test Infrastructure

The integration tests use [Testcontainers](https://testcontainers.com/) to spin up Docker containers for:

- **PostgreSQL**: Stores backup metadata and file entries (via Marten)
- **Qdrant**: Vector database for storing indexed content embeddings

Each test gets fresh, isolated containers that are automatically cleaned up after the test completes.

## Running the Tests

### Run all integration tests
```bash
dotnet test Indexer.IntegrationTests/Indexer.IntegrationTests.csproj
```

### Run with detailed output
```bash
dotnet test Indexer.IntegrationTests/Indexer.IntegrationTests.csproj --logger "console;verbosity=detailed"
```

### Run a specific test
```bash
dotnet test Indexer.IntegrationTests/Indexer.IntegrationTests.csproj --filter "FullyQualifiedName~BackupCreate_Modify_CreateNewBackup"
```

### Skip Docker tests (if Docker is not available)
The tests will be skipped automatically if Docker is not available.

## Test Structure

### Fixtures
- **[`PostgreSqlFixture.cs`](Fixtures/PostgreSqlFixture.cs)**: Manages PostgreSQL container lifecycle
- **[`QdrantFixture.cs`](Fixtures/QdrantFixture.cs)**: Manages Qdrant container lifecycle
- **[`IntegrationTestCollection.cs`](Fixtures/IntegrationTestCollection.cs)**: XUnit collection to share fixtures across tests

### Helpers
- **[`DuplicatiCliHelper.cs`](Helpers/DuplicatiCliHelper.cs)**: Wrapper for running Duplicati CLI commands

### Test Classes
- **[`BackupIntegrationTests.cs`](BackupIntegrationTests.cs)**: Main integration tests

## Test Scenarios

### 1. Backup Creation, Indexing, and Diff Calculation
**Test**: `BackupCreate_Modify_CreateNewBackup_DiffCalculator_FindsChanges`

This test:
1. Creates test files in a temporary directory
2. Creates a Duplicati backup (Version 1 with timestamp from dlist filename)
3. Creates a BackupSource with TargetUrl and EncryptionPassword
4. Processes the backup's dlist file and stores entries in PostgreSQL
5. Modifies some files and deletes others
6. Creates a second backup (Version 2 with later timestamp)
7. Processes the second dlist file
8. Uses the DiffCalculator to identify changed files between versions
9. Verifies the correct files are identified for indexing

### 2. Encrypted Backup Processing
**Test**: `DlistProcessor_ProcessesEncryptedBackup_Correctly`

This test:
1. Creates a BackupSource with EncryptionPassword configured
2. Creates an encrypted backup with a passphrase
3. Processes the encrypted dlist file
4. Verifies the backup contents are correctly decrypted and stored

### 3. Multiple Version Diff Calculation
**Test**: `DiffCalculator_WithMultipleVersions_IdentifiesCorrectChanges`

This test:
1. Creates three backup versions with different timestamps (from dlist filenames)
2. Creates a BackupSource and processes all dlist files
3. Calculates diffs between consecutive versions using DateTimeOffset timestamps
4. Verifies correct change detection for each transition

## Version Format

The backup version is extracted from the dlist filename using the format:
`duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]`

For example, `duplicati-20240312T143000Z.dlist.zip` represents version timestamp 2024-03-12 14:30:00 UTC.

The `BackupVersionCreated` message only contains:
- `BackupId`: The backup identifier
- `DlistFilename`: The dlist filename (version is extracted from this)

The handler looks up the `BackupSource` to get the `TargetUrl` and `EncryptionPassword`.

## Docker Configuration

### PostgreSQL Container
- Image: `postgres:16-alpine`
- Database: `indexer_test`
- User: `postgres`
- Password: `testpassword`

### Qdrant Container
- Image: `qdrant/qdrant:latest`
- Ports: 6333 (HTTP), 6334 (gRPC)
- Health check: HTTP GET on `/healthz`

## Troubleshooting

### Docker Not Available
If you see errors about Docker not being available:
- Ensure Docker Desktop or Docker Engine is running
- Check that you have permissions to access Docker

### Duplicati CLI Not Found
If the tests fail to find Duplicati CLI:
1. Install Duplicati from https://www.duplicati.com/
2. Or build from source: `dotnet build modules/duplicati/Executables/Duplicati.CommandLine -c Release`
3. Ensure `duplicati-cli` is in your PATH

### Port Conflicts
If tests fail due to port conflicts:
- Testcontainers uses random ports for container bindings
- If you have services using common ports (5432, 6333), tests should still work
- Docker port binding uses ephemeral ports

## CI/CD Integration

For running in CI/CD pipelines (GitHub Actions, Azure DevOps, etc.):

```yaml
# Example GitHub Actions workflow
- name: Run Integration Tests
  run: dotnet test Indexer.IntegrationTests/Indexer.IntegrationTests.csproj
```

The tests require Docker-in-Docker support in your CI environment.

## Extending the Tests

To add new integration tests:

1. Create a new test class and add the `[Collection("IntegrationTests")]` attribute
2. Inject the fixtures in the constructor:
   ```csharp
   public MyTests(PostgreSqlFixture postgresFixture, QdrantFixture qdrantFixture, ITestOutputHelper output)
   ```
3. Use the `DuplicatiCliHelper` to interact with Duplicati
4. Create a service provider with test configuration:
   ```csharp
   var services = new ServiceCollection();
   services.AddMarten(options => {
       MartenConfiguration.Configure(options, _postgresFixture.ConnectionString);
   });
   // ... add other services
   var serviceProvider = services.BuildServiceProvider();
   ```

## Performance Considerations

- Each test class gets fresh containers (takes ~10-20 seconds to start)
- Tests within the same class share containers
- Container cleanup happens automatically after tests complete
- Consider using `ICollectionFixture` for sharing expensive resources
