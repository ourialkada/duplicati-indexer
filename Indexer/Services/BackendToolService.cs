using System.Diagnostics;
using System.Text;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Service for downloading files from Duplicati backends using the duplicati-backend-tool CLI.
/// </summary>
public class BackendToolService
{
    private readonly ILogger<BackendToolService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendToolService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public BackendToolService(ILogger<BackendToolService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Downloads a file from a Duplicati backend to a local path.
    /// Uses the duplicati-backend-tool GET command.
    /// </summary>
    /// <param name="backendUrl">The Duplicati backend URL (e.g., "s3://bucket/path" or "file:///backups").</param>
    /// <param name="remoteFilename">The remote filename to download.</param>
    /// <param name="localFilePath">The local file path to save the downloaded file to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the download fails.</exception>
    public async Task DownloadFileAsync(
        string backendUrl,
        string remoteFilename,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backendUrl))
        {
            throw new ArgumentException("Backend URL cannot be null or empty.", nameof(backendUrl));
        }

        if (string.IsNullOrWhiteSpace(remoteFilename))
        {
            throw new ArgumentException("Remote filename cannot be null or empty.", nameof(remoteFilename));
        }

        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));
        }

        _logger.LogInformation("Downloading {RemoteFilename} from {BackendUrl} to {LocalFilePath}",
            remoteFilename, backendUrl, localFilePath);

        // Ensure the local directory exists
        var localDir = Path.GetDirectoryName(localFilePath)!;
        if (!Directory.Exists(localDir))
        {
            Directory.CreateDirectory(localDir);
        }

        // Build command: GET <backend-url> <local-file-path>
        // The backend-tool uses Path.GetFileName(args[2]) as the remote name
        // and args[2] as the local destination path.
        var resolved = DuplicatiPathResolver.FindBackendTool();
        var processStartInfo = resolved.CreateProcessStartInfo("GET", backendUrl, localFilePath);

        _logger.LogDebug("Executing backend-tool: {FileName} {Args}",
            processStartInfo.FileName,
            string.Join(" ", processStartInfo.ArgumentList));

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogTrace("BackendTool Output: {Data}", e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("BackendTool Error: {Data}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            var output = outputBuilder.ToString();
            _logger.LogError("Backend-tool GET failed with exit code {ExitCode}. Output: {Output} Error: {Error}",
                process.ExitCode, output, error);
            throw new InvalidOperationException(
                $"Failed to download '{remoteFilename}' from '{backendUrl}'. Exit code: {process.ExitCode}. Error: {error}. Output: {output}");
        }

        if (!File.Exists(localFilePath))
        {
            throw new InvalidOperationException(
                $"Backend-tool reported success but file was not found at '{localFilePath}'.");
        }

        _logger.LogInformation("Successfully downloaded {RemoteFilename} to {LocalFilePath} ({Size} bytes)",
            remoteFilename, localFilePath, new FileInfo(localFilePath).Length);
    }
}
