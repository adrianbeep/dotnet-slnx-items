using DotnetSlnxItems.Cli.Tests.TestHelpers;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the root <c>slnx-items</c> invocation
/// (GAP 5 from the test matrix coverage audit, issue #14):
/// no-args, <c>--help</c>, and <c>--version</c>. The CLI's main entry
/// point is wired in <c>src/DotnetSlnxItems.Cli/Program.cs</c>; the
/// <c>--version</c> option was added by #12 to replace the default
/// <c>System.CommandLine</c> version output. The 4 subcommands
/// (<c>add</c>, <c>remove</c>, <c>list</c>, <c>sync</c>) are wired at
/// <c>Program.cs:58-70</c>.
///
/// Each test runs the CLI as a subprocess from a fresh temp cwd via
/// <see cref="CliRunner"/>. Per-test temp dirs are tracked in
/// <see cref="_tempDirs"/> and removed on disposal (best effort).
/// </summary>
public class RootCommandTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (string dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best effort
            }
        }
        _tempDirs.Clear();
    }

    [Fact]
    public void Root_NoArgs_ExitsOneAndStderrSaysMissingSubcommand()
    {
        CliResult result = RunCli(Array.Empty<string>());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("slnx-items: missing subcommand. Use --help for usage.", result.Stderr);
    }

    [Fact]
    public void Root_Help_ExitsZeroAndListsAllFourSubcommands()
    {
        CliResult result = RunCli("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("add", result.StdOut);
        Assert.Contains("remove", result.StdOut);
        Assert.Contains("list", result.StdOut);
        Assert.Contains("sync", result.StdOut);
    }

    [Fact]
    public void Root_Version_ExitsZeroAndStdoutNamesVersion()
    {
        CliResult result = RunCli("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("slnx-items 0.1.1", result.StdOut);
    }

    // ---- Helpers ----

    private CliResult RunCli(params string[] args)
    {
        string tempDir = NewTempDir();
        try
        {
            return CliRunner.RunWithCwd(tempDir, args);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Fail(ex.Message);
            throw;
        }
    }

    private string NewTempDir()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "DotnetSlnItems-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }
}