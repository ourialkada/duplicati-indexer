using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Indexer.IntegrationTests.Fixtures;

/// <summary>
/// Fixture that provides a Qdrant vector database container for integration tests.
/// </summary>
public class QdrantFixture : IAsyncLifetime
{
    private IContainer? _qdrantContainer;

    /// <summary>
    /// Gets the HTTP endpoint URI for the Qdrant container.
    /// </summary>
    public Uri? Endpoint { get; private set; }

    /// <summary>
    /// Starts the Qdrant container.
    /// </summary>
    public async Task InitializeAsync()
    {
        _qdrantContainer = new ContainerBuilder()
            .WithImage("qdrant/qdrant:latest")
            .WithPortBinding(6333, true)
            .WithPortBinding(6334, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request =>
                    request.ForPort(6333)
                           .ForPath("/healthz")))
            .Build();

        await _qdrantContainer.StartAsync();

        var host = _qdrantContainer.Hostname;
        var port = _qdrantContainer.GetMappedPublicPort(6333);
        Endpoint = new Uri($"http://{host}:{port}");
    }

    /// <summary>
    /// Stops and disposes the Qdrant container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_qdrantContainer != null)
        {
            await _qdrantContainer.DisposeAsync();
        }
    }
}
