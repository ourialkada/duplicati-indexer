using System.Diagnostics;

namespace DuplicatiIndexer.Services;

/// <summary>
/// Resolves paths to Duplicati CLI executables across different environments
/// (Docker container, local development with source build, system-installed).
/// </summary>
public static class DuplicatiPathResolver
{
    /// <summary>
    /// Represents a resolved executable that can be used to configure a <see cref="ProcessStartInfo"/>.
    /// </summary>
    /// <param name="FileName">The process filename (either the executable path or "dotnet" for .dll files).</param>
    /// <param name="PrefixArgs">Any arguments that must be prepended before the user's arguments (e.g., the .dll path).</param>
    public record ResolvedExecutable(string FileName, string[] PrefixArgs)
    {
        /// <summary>
        /// Configures a <see cref="ProcessStartInfo"/> with the resolved executable and the given arguments.
        /// </summary>
        /// <param name="arguments">The command arguments to pass to the executable.</param>
        /// <returns>A configured <see cref="ProcessStartInfo"/>.</returns>
        public ProcessStartInfo CreateProcessStartInfo(params string[] arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = FileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var prefixArg in PrefixArgs)
            {
                psi.ArgumentList.Add(prefixArg);
            }

            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            return psi;
        }
    }

    /// <summary>
    /// Finds the Duplicati CommandLine (duplicati-cli) executable.
    /// </summary>
    /// <returns>The resolved executable information.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the executable cannot be found.</exception>
    public static ResolvedExecutable FindCommandLine()
    {
        return FindExecutable(
            "Duplicati.CommandLine",
            "Duplicati.CommandLine",
            "duplicati-cli");
    }

    /// <summary>
    /// Finds the Duplicati BackendTool executable.
    /// </summary>
    /// <returns>The resolved executable information.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the executable cannot be found.</exception>
    public static ResolvedExecutable FindBackendTool()
    {
        return FindExecutable(
            "Duplicati.CommandLine.BackendTool",
            "Duplicati.CommandLine.BackendTool",
            "duplicati-backend-tool");
    }

    /// <summary>
    /// Finds a Duplicati executable by searching multiple candidate locations.
    /// </summary>
    /// <param name="executableName">The base executable name without extension (e.g., "Duplicati.CommandLine.BackendTool").</param>
    /// <param name="projectName">The project directory name under Executables/ (e.g., "Duplicati.CommandLine.BackendTool").</param>
    /// <param name="linuxAlias">The Linux/macOS alias name (e.g., "duplicati-backend-tool").</param>
    /// <returns>The resolved executable information.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the executable cannot be found in any candidate location.</exception>
    private static ResolvedExecutable FindExecutable(string executableName, string projectName, string linuxAlias)
    {
        var candidates = GetCandidatePaths(executableName, projectName, linuxAlias);

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return ToResolvedExecutable(candidate);
            }
        }

        // As a last resort, check if the platform-appropriate name is on PATH
        var fallback = OperatingSystem.IsWindows() ? $"{executableName}.exe" : linuxAlias;

        if (IsOnPath(fallback))
        {
            return new ResolvedExecutable(fallback, []);
        }

        throw new FileNotFoundException(
            $"Could not find Duplicati executable '{executableName}'. " +
            $"Searched locations: {string.Join(", ", candidates)}. " +
            $"Also checked PATH for '{fallback}'.");
    }

    /// <summary>
    /// Converts a found file path into a <see cref="ResolvedExecutable"/>.
    /// If the path is a .dll, it will be run via "dotnet".
    /// </summary>
    private static ResolvedExecutable ToResolvedExecutable(string path)
    {
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedExecutable("dotnet", [path]);
        }

        return new ResolvedExecutable(path, []);
    }

    /// <summary>
    /// Gets candidate file paths to search for the executable.
    /// </summary>
    private static List<string> GetCandidatePaths(string executableName, string projectName, string linuxAlias)
    {
        var candidates = new List<string>();
        var isWindows = OperatingSystem.IsWindows();
        var exeName = isWindows ? $"{executableName}.exe" : executableName;

        // 1. Docker container paths (published executables)
        candidates.Add(Path.Combine("/app", linuxAlias, exeName));

        // 2. Well-known symlink locations (set up in Dockerfile)
        if (!isWindows)
        {
            candidates.Add(Path.Combine("/usr/local/bin", linuxAlias));
        }

        // 3. Relative to the application base directory (for local development)
        var appBaseDir = AppContext.BaseDirectory;

        // Direct sibling (e.g., if both tools are published to the same directory)
        candidates.Add(Path.Combine(appBaseDir, exeName));

        // 4. Built from source - search relative to solution root
        var solutionDir = FindSolutionDirectory();
        if (solutionDir != null)
        {
            // Release build output
            candidates.Add(Path.Combine(solutionDir, "modules", "duplicati",
                "Executables", projectName, "bin", "Release", "net10.0", exeName));

            // Debug build output
            candidates.Add(Path.Combine(solutionDir, "modules", "duplicati",
                "Executables", projectName, "bin", "Debug", "net10.0", exeName));

            // Also check for .dll variant (when UseAppHost=false)
            var dllName = $"{executableName}.dll";
            candidates.Add(Path.Combine(solutionDir, "modules", "duplicati",
                "Executables", projectName, "bin", "Release", "net10.0", dllName));
            candidates.Add(Path.Combine(solutionDir, "modules", "duplicati",
                "Executables", projectName, "bin", "Debug", "net10.0", dllName));
        }

        // 5. Windows Program Files
        if (isWindows)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(programFiles, "Duplicati 2", exeName));
        }

        return candidates;
    }

    /// <summary>
    /// Finds the solution directory by traversing up from the application base directory.
    /// </summary>
    private static string? FindSolutionDirectory()
    {
        var currentDir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(currentDir))
        {
            if (File.Exists(Path.Combine(currentDir, "DuplicatiIndexer.slnx")) ||
                File.Exists(Path.Combine(currentDir, "DuplicatiIndexer.sln")))
            {
                return currentDir;
            }

            var parentDir = Directory.GetParent(currentDir);
            currentDir = parentDir?.FullName;
        }

        return null;
    }

    /// <summary>
    /// Checks if an executable is available on the system PATH.
    /// </summary>
    private static bool IsOnPath(string executable)
    {
        try
        {
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            return pathDirs.Any(dir => File.Exists(Path.Combine(dir, executable)));
        }
        catch
        {
            return false;
        }
    }
}
