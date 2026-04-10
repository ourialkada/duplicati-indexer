using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Indexer.IntegrationTests.Helpers;

/// <summary>
/// Helper class for running Duplicati CLI commands.
/// </summary>
public class DuplicatiCliHelper
{
    private readonly string _executablePath;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicatiCliHelper"/> class.
    /// </summary>
    /// <param name="output">The test output helper for logging.</param>
    public DuplicatiCliHelper(ITestOutputHelper output)
    {
        _output = output;
        _executablePath = FindDuplicatiExecutable();
    }

    /// <summary>
    /// Finds the Duplicati CLI executable path. Builds from source if not already built.
    /// </summary>
    private string FindDuplicatiExecutable()
    {
        // Get the path to the modules duplicati project
        var solutionDirectory = GetSolutionDirectory();
        var projectPath = Path.Combine(solutionDirectory, "modules", "duplicati",
            "Executables", "Duplicati.CommandLine", "Duplicati.CommandLine.csproj");
        var outputDirectory = Path.Combine(solutionDirectory, "modules", "duplicati",
            "Executables", "Duplicati.CommandLine", "bin", "Release", "net10.0");

        var executableName = OperatingSystem.IsWindows() ? "Duplicati.CommandLine.exe" : "Duplicati.CommandLine";
        var executablePath = Path.Combine(outputDirectory, executableName);
        var dllPath = Path.Combine(outputDirectory, "Duplicati.CommandLine.dll");

        // Check if release build already exists
        if (File.Exists(executablePath) || File.Exists(dllPath))
        {
            _output.WriteLine($"Using existing Duplicati CLI build at: {outputDirectory}");
            return File.Exists(executablePath) ? executablePath : dllPath;
        }

        // Build the project in release mode
        _output.WriteLine("Duplicati CLI not found. Building from source...");
        BuildDuplicati(projectPath, outputDirectory);

        // Verify the build succeeded
        if (File.Exists(executablePath))
        {
            return executablePath;
        }
        else if (File.Exists(dllPath))
        {
            return dllPath;
        }

        throw new InvalidOperationException(
            $"Failed to build Duplicati CLI. Expected executable not found at: {executablePath}");
    }

    /// <summary>
    /// Gets the solution root directory by traversing up from the test assembly location.
    /// </summary>
    private static string GetSolutionDirectory()
    {
        // Start from the current assembly location and traverse up to find the solution root
        var currentDir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(currentDir))
        {
            // Check for solution file
            if (File.Exists(Path.Combine(currentDir, "DuplicatiIndexer.slnx")) ||
                File.Exists(Path.Combine(currentDir, "DuplicatiIndexer.sln")))
            {
                return currentDir;
            }

            var parentDir = Directory.GetParent(currentDir);
            currentDir = parentDir?.FullName;
        }

        // Fallback to current directory if solution not found
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Builds the Duplicati CLI project in Release mode.
    /// </summary>
    private static void BuildDuplicati(string projectPath, string outputDirectory)
    {
        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Duplicati project file not found at: {projectPath}");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" -c Release -o \"{outputDirectory}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to build Duplicati CLI: {error}");
        }
    }

    /// <summary>
    /// Creates a backup using Duplicati CLI.
    /// </summary>
    /// <param name="sourcePath">The source directory to backup.</param>
    /// <param name="targetPath">The target backup destination.</param>
    /// <param name="passphrase">Optional encryption passphrase.</param>
    /// <returns>The backup version number.</returns>
    public async Task<int> BackupAsync(string sourcePath, string targetPath, string? passphrase = null)
    {
        var arguments = new List<string>
        {
            "backup",
            targetPath,
            sourcePath,
            "--no-encryption=false",
            "--backup-name=IntegrationTestBackup"
        };

        if (!string.IsNullOrEmpty(passphrase))
        {
            arguments.Add($"--passphrase={passphrase}");
        }

        var result = await RunCommandAsync(arguments);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Backup failed: {result.Error}");
        }

        // Parse the version number from output
        // Duplicati typically outputs: "Backup completed successfully! Version: X"
        var versionLine = result.Output.Split('\n')
            .FirstOrDefault(l => l.Contains("Version:", StringComparison.OrdinalIgnoreCase));

        if (versionLine != null)
        {
            var versionStr = versionLine.Split(':').LastOrDefault()?.Trim();
            if (int.TryParse(versionStr, out var version))
            {
                return version;
            }
        }

        // If we can't parse the version, return 0 (first backup)
        return 0;
    }

    /// <summary>
    /// Lists available backup versions.
    /// </summary>
    /// <param name="targetPath">The backup destination.</param>
    /// <param name="passphrase">Optional encryption passphrase.</param>
    /// <returns>A list of backup versions.</returns>
    public async Task<List<int>> ListVersionsAsync(string targetPath, string? passphrase = null)
    {
        var arguments = new List<string>
        {
            "list",
            targetPath,
            "--no-encryption=false"
        };

        if (!string.IsNullOrEmpty(passphrase))
        {
            arguments.Add($"--passphrase={passphrase}");
        }

        var result = await RunCommandAsync(arguments);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"List failed: {result.Error}");
        }

        var versions = new List<int>();
        foreach (var line in result.Output.Split('\n'))
        {
            // Parse version lines - format: "0\t: 2024-01-15 10:30:00 ..." or "  0 : 2024-01-15 10:30:00 ..."
            var trimmedLine = line.TrimStart();
            if (trimmedLine.Length > 0 && char.IsDigit(trimmedLine[0]))
            {
                // Extract just the number before any non-digit character
                var versionPart = new string(trimmedLine.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(versionPart, out var version))
                {
                    versions.Add(version);
                }
            }
        }

        return versions;
    }

    /// <summary>
    /// Gets the path to a dlist file for a specific backup version.
    /// </summary>
    /// <param name="targetPath">The backup destination.</param>
    /// <param name="version">The backup version index.</param>
    /// <returns>The path to the dlist file.</returns>
    public string GetDlistFilePath(string targetPath, int version)
    {
        // dlist files are typically named: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip.aes
        var backupDir = targetPath.Replace("file://", "").Replace("file:", "");
        var files = Directory.GetFiles(backupDir, "*.dlist.zip*");

        // Sort by creation time and pick the appropriate one
        var sorted = files
            .Select(f => new { Path = f, Time = File.GetCreationTime(f) })
            .OrderBy(f => f.Time)
            .ToList();

        if (version < sorted.Count)
        {
            return sorted[version].Path;
        }

        // Return the most recent if version is out of range
        return sorted.LastOrDefault()?.Path
            ?? throw new InvalidOperationException("No dlist files found in backup directory");
    }

    // Regex to extract timestamp from dlist filename: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]
    private static readonly Regex DlistFilenameRegex = new(
        @"duplicati-(\d{8}T\d{6}Z)\.dlist",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the version timestamp from a dlist filename.
    /// </summary>
    /// <param name="dlistFilePath">The path to the dlist file.</param>
    /// <returns>The parsed DateTimeOffset.</returns>
    public static DateTimeOffset ExtractVersionFromDlistPath(string dlistFilePath)
    {
        var filename = Path.GetFileName(dlistFilePath);
        var match = DlistFilenameRegex.Match(filename);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Invalid dlist filename format: '{filename}'. Expected format: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]",
                nameof(dlistFilePath));
        }

        var timestampStr = match.Groups[1].Value;
        if (!DateTimeOffset.TryParseExact(
            timestampStr,
            "yyyyMMddTHHmmssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var version))
        {
            throw new ArgumentException(
                $"Failed to parse timestamp from dlist filename: '{filename}'",
                nameof(dlistFilePath));
        }

        return version;
    }

    /// <summary>
    /// Runs a Duplicati CLI command.
    /// </summary>
    private async Task<CommandResult> RunCommandAsync(List<string> arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        _output.WriteLine($"Running: {_executablePath} {string.Join(" ", arguments)}");

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _output.WriteLine($"[OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _output.WriteLine($"[ERR] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };
    }

    private class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
