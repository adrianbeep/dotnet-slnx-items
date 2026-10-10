using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DotnetSlnxItems.Cli.Tests.TestHelpers;

/// <summary>
/// Spawns the CLI under test as a subprocess so each test runs in a
/// fresh working directory with a real exit code, real stdout, and
/// real stderr. In-process invocation via
/// <c>System.CommandLine.InvokeAsync</c> was rejected because the
/// spec mandates exit-code and stderr assertions; only a subprocess
/// exercises the real <c>Main</c> through the apphost.
/// </summary>
internal static class CliRunner
{
    private const string RepoRootMarker = "DotnetSlnxItems.slnx";

    /// <summary>
    /// Runs the CLI from the test runner's current directory with
    /// the given arguments. The captured result includes the exit
    /// code and both streams. A missing apphost surfaces as
    /// <see cref="InvalidOperationException"/>; tests can wrap the
    /// call in <c>try { CliRunner.Run(...); } catch
    /// (InvalidOperationException ex) { Assert.Fail(ex.Message); }
    /// </c> to surface the helpful discovery message instead of a
    /// cryptic framework failure.
    /// </summary>
    public static CliResult Run(params string[] args)
    {
        string cwd = Environment.CurrentDirectory;
        return RunWithCwd(cwd, args);
    }

    /// <summary>
    /// Runs the CLI from <paramref name="workingDirectory"/> with
    /// the given arguments. Same exception contract as
    /// <see cref="Run"/>.
    /// </summary>
    public static CliResult RunWithCwd(string workingDirectory, params string[] args)
    {
        string apphost = DiscoverCliApphostPath();

        var startInfo = new ProcessStartInfo
        {
            FileName = apphost,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new CliResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until it
    /// finds <c>DotnetSlnxItems.slnx</c> (the repo-root marker), then
    /// searches the four canonical build-output paths under
    /// <c>src/DotnetSlnxItems.Cli/bin/</c>. First existing candidate
    /// wins. On miss, throws an exception that names every path it
    /// looked at so the test author can fix the environment with a
    /// single <c>dotnet build</c>.
    /// </summary>
    private static string DiscoverCliApphostPath()
    {
        string startDir = AppContext.BaseDirectory;
        string? repoRoot = FindRepoRoot(startDir);
        if (repoRoot is null)
        {
            throw new InvalidOperationException(
                $"CliRunner could not locate the repo root by walking up from '{startDir}' " +
                $"looking for '{RepoRootMarker}'. Run tests from the repository root.");
        }

        string exeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        string apphostName = "dotnet-slnx-items" + exeSuffix;

        string[] configurations = { "Debug", "Release" };
        string[] targetFrameworks = { "net8.0", "net10.0" };

        var searched = new List<string>();
        foreach (string config in configurations)
        {
            foreach (string tfm in targetFrameworks)
            {
                string candidate = Path.Combine(
                    repoRoot,
                    "src",
                    "DotnetSlnxItems.Cli",
                    "bin",
                    config,
                    tfm,
                    apphostName);
                searched.Add(candidate);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException(
            "CliRunner could not locate the built CLI apphost. " +
            "Searched the following paths:\n  - " +
            string.Join("\n  - ", searched) +
            "\nRun `dotnet build src/DotnetSlnxItems.Cli` to produce one of these binaries, " +
            "then re-run the tests.");
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        DirectoryInfo? dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            string marker = Path.Combine(dir.FullName, RepoRootMarker);
            if (File.Exists(marker))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}

/// <summary>
/// Captured result of a single CLI invocation: exit code plus the
/// two captured streams, ready for substring assertions.
/// </summary>
internal sealed record CliResult(int ExitCode, string StdOut, string Stderr);