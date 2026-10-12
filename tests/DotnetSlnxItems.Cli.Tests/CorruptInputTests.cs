using DotnetSlnxItems.Cli.Tests.TestHelpers;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests covering corrupt / malformed solution input
/// across all 6 mutating + read commands and both supported formats
/// (.sln, .slnx). GAP 3 from the test matrix coverage audit (issue #14).
///
/// The persistence library is strict at the model level: a corrupt
/// file (missing required header in .sln, truncated mid-element in
/// .slnx) is rejected with <c>SolutionItemsException(PersistenceError)</c>
/// before any model code touches it. The CLI then maps that to exit
/// 1 with the Core-translated message on stderr. The tests assert the
/// CLI does NOT leak a stack trace to the user — no <c>Exception:</c>
/// and no <c>at </c> markers (the LINQ stack frame token).
///
/// Fixtures:
/// - corrupt.sln: missing the <c>Microsoft Visual Studio Solution File,
///   Format Version 12.00</c> header line. Core throws
///   <c>SolutionItemsException(PersistenceError, "Not a solution file.")</c>.
/// - corrupt.slnx: truncated mid-element. Core throws
///   <c>SolutionItemsException(PersistenceError, "Failed to load
///   solution '...': Unexpected end of file has occurred. ...")</c>.
/// </summary>
public class CorruptInputTests : IDisposable
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

    // ---- add file ----

    [Fact]
    public void AddFile_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.sln");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        AssertCorrupt(run);
    }

    [Fact]
    public void AddFile_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.slnx");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        AssertCorrupt(run);
    }

    // ---- add folder ----

    [Fact]
    public void AddFolder_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.sln"),
            "add", "folder", "Build");

        AssertCorrupt(run);
    }

    [Fact]
    public void AddFolder_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.slnx"),
            "add", "folder", "Build");

        AssertCorrupt(run);
    }

    // ---- remove file ----

    [Fact]
    public void RemoveFile_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.sln");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "remove", "file", "README.md");

        AssertCorrupt(run);
    }

    [Fact]
    public void RemoveFile_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.slnx");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "remove", "file", "README.md");

        AssertCorrupt(run);
    }

    // ---- remove folder ----

    [Fact]
    public void RemoveFolder_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.sln"),
            "remove", "folder", "Build");

        AssertCorrupt(run);
    }

    [Fact]
    public void RemoveFolder_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.slnx"),
            "remove", "folder", "Build");

        AssertCorrupt(run);
    }

    // ---- list ----

    [Fact]
    public void List_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.sln"),
            "list");

        AssertCorrupt(run);
    }

    [Fact]
    public void List_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "corrupt.slnx"),
            "list");

        AssertCorrupt(run);
    }

    // ---- sync ----

    [Fact]
    public void Sync_CorruptSln_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.sln");
                CopyFixture(dir, "slnx-items-empty.yaml");
            },
            "sync", "--manifest", "slnx-items-empty.yaml");

        AssertCorrupt(run);
    }

    [Fact]
    public void Sync_CorruptSlnx_ExitsNonZeroWithoutStackTrace()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "corrupt.slnx");
                CopyFixture(dir, "slnx-items-empty.yaml");
            },
            "sync", "--manifest", "slnx-items-empty.yaml");

        AssertCorrupt(run);
    }

    // ---- Helpers ----

    private sealed record CliRun(CliResult Result, string PrimaryFixturePath);

    /// <summary>
    /// Shared assertion for every corrupt-input scenario: exit code
    /// is non-zero, stderr is non-empty, and stderr does NOT include
    /// a stack trace leak (<c>Exception:</c> or <c>at </c> markers).
    /// </summary>
    private static void AssertCorrupt(CliRun run)
    {
        Assert.NotEqual(0, run.Result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(run.Result.Stderr));
        Assert.DoesNotContain("Exception:", run.Result.Stderr);
        Assert.DoesNotContain("at ", run.Result.Stderr);
    }

    private CliRun RunInTempDir(Action<string> setup, params string[] args)
    {
        return RunInTempDirAsync(dir => { setup(dir); return Task.CompletedTask; }, args).GetAwaiter().GetResult();
    }

    private CliRun RunInTempDir(Func<string, Task> setup, params string[] args)
    {
        return RunInTempDirAsync(setup, args).GetAwaiter().GetResult();
    }

    private async Task<CliRun> RunInTempDirAsync(Func<string, Task> setup, params string[] args)
    {
        string tempDir = NewTempDir();
        await setup(tempDir).ConfigureAwait(false);

        CliResult result;
        try
        {
            result = CliRunner.RunWithCwd(tempDir, args);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Fail(ex.Message);
            throw;
        }

        string primaryFixture = Directory.EnumerateFiles(tempDir)
            .FirstOrDefault(f =>
                f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            ?? tempDir;

        return new CliRun(result, primaryFixture);
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

    private string CopyFixture(string targetDir, params string[] names)
    {
        string sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        string firstTarget = string.Empty;
        foreach (string name in names)
        {
            string source = Path.Combine(sourceDir, name);
            string target = Path.Combine(targetDir, name);
            File.Copy(source, target, overwrite: true);
            if (firstTarget.Length == 0)
            {
                firstTarget = target;
            }
        }
        return firstTarget;
    }
}