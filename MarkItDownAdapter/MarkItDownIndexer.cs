using System.Diagnostics;
using System.Text;
using DuplicatiIndexer.AdapterInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DuplicatiIndexer.MarkItDownAdapter;

/// <summary>
/// Implementation of IContentIndexer using Microsoft's MarkItDown tool for text extraction.
/// MarkItDown is a Python CLI tool that converts various file formats to markdown/plain text.
/// </summary>
public class MarkItDownIndexer : IContentIndexer
{
    private readonly ILogger<MarkItDownIndexer> _logger;
    private readonly long _maxFileSizeBytes;
    private readonly string _markitdownPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDownIndexer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public MarkItDownIndexer(ILogger<MarkItDownIndexer> logger, IConfiguration configuration)
    {
        _logger = logger;
        _maxFileSizeBytes = configuration.GetValue<long>("Indexing:MaxFileSizeBytes", 104857600); // Default 100MB
        _markitdownPath = configuration.GetValue<string>("MarkItDown:ExecutablePath", "markitdown");
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting text from {FilePath} via MarkItDown", filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            _logger.LogError("File not found: {FilePath}", filePath);
            throw new FileNotFoundException("File not found", filePath);
        }

        if (fileInfo.Length > _maxFileSizeBytes)
        {
            _logger.LogWarning("File {FilePath} exceeds maximum size limit of {MaxSize} bytes. Size: {ActualSize}",
                filePath, _maxFileSizeBytes, fileInfo.Length);
            throw new InvalidOperationException($"File size exceeds maximum limit of {_maxFileSizeBytes} bytes.");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _markitdownPath,
            Arguments = $"\"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = processStartInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var errorOutput = errorBuilder.ToString();
                _logger.LogError("MarkItDown failed with exit code {ExitCode}. Error: {Error}",
                    process.ExitCode, errorOutput);
                throw new InvalidOperationException($"MarkItDown failed to extract text from {filePath}. Error: {errorOutput}");
            }

            var result = outputBuilder.ToString().Trim();
            _logger.LogInformation("Extracted {CharCount} characters from {FilePath}", result.Length, filePath);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to execute MarkItDown for {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Checks if MarkItDown is available and working.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if MarkItDown is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _markitdownPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
