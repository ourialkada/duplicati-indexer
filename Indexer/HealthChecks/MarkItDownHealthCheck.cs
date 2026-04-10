using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DuplicatiIndexer.HealthChecks;

/// <summary>
/// Health check for MarkItDown availability.
/// </summary>
public class MarkItDownHealthCheck : IHealthCheck
{
    private readonly ILogger<MarkItDownHealthCheck> _logger;
    private readonly string _markitdownPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDownHealthCheck"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public MarkItDownHealthCheck(ILogger<MarkItDownHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;
        _markitdownPath = configuration.GetValue<string>("MarkItDown:ExecutablePath", "markitdown");
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
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

            if (process.ExitCode == 0)
            {
                var version = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                return HealthCheckResult.Healthy($"MarkItDown is available: {version.Trim()}");
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                return HealthCheckResult.Unhealthy($"MarkItDown returned exit code {process.ExitCode}. Error: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkItDown health check failed");
            return HealthCheckResult.Unhealthy("MarkItDown is not available", ex);
        }
    }
}
