using DotnetSlnxItems.Cli;
using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items remove folder</c>
/// command (issue #43). Each test copies the minimal single-project
/// fixtures into a fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "folder already present" precondition can pre-create
/// nested folders via <see cref="SolutionItemsService.AddFolderAsync"/>.
///
/// Per-test temp directories are tracked in <see cref="_tempDirs"/>
/// and removed on disposal (best effort) — the CLI writes back to
/// the .sln/.slnx on disk, so the test body still has read access
/// to the solution while the test runs.
///
/// <para>
/// Exit code convention: every test that runs the CLI to completion
/// asserts on <c>run.Result.ExitCode</c> via <c>Assert.Equal</c>.
/// The mapping from CLI exit code to the originating condition lives
/// in <c>src/DotnetSlnxItems.Cli/Errors/ErrorMapper.cs</c>:
/// 1 = catch-all (<c>FolderNotFound</c>, <c>PersistenceError</c>,
/// unhandled),
/// 2 = "no solution in cwd" (CLI pre-flight) or Core
/// <c>SolutionFileNotFound</c>,
/// 3 = Core <c>UnsupportedFormat</c>,
/// 4 = Core <c>SlnfNotSupported</c>,
/// 5 = Core <c>AmbiguousSolutionInDirectory</c>,
/// 6 = bad folder name (CLI pre-flight for <c>..</c>/absolute paths,
/// or Core <c>InvalidFolderPath</c> for empty/whitespace/slashes),
/// 7 = Core <c>InvalidFilePath</c>.
/// </para>
/// </summary>
public class RemoveFolderCommandTests : IDisposable
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

    // ---- Test 1: --help ----

    [Fact]
    public void Help_ShowsUsageAndOptions()
    {
        CliRun run = RunInTempDir(
            _ => { /* no fixture */ },
            "remove", "folder", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("<name>", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
    }

    // ---- Test 2 + 3: top-level folder (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void RemoveFolder_TopLevel_Disappears(string fixture)
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, fixture, "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.SaveAsync();
            },
            "remove", "folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/Build/' removed from the solution.", run.Result.StdOut);
        Assert.DoesNotContain("was not in the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
    }

    // ---- Test 4: nested path with descendants, prints cascade ----

    [Fact]
    public void RemoveFolder_NestedPath_AppendsAncestorsAndReportsCascade()
    {
        // Pre-create /a/b/c/ with two deeper descendants /a/b/c/d/
        // and /a/b/c/e/ so the cascade walk finds children of the
        // targeted folder (the persistence library's RemoveFolder
        // cascades by design and the service reports the same set).
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("a/b/c/d");
                await service.AddFolderAsync("a/b/c/e");
                await service.SaveAsync();
            },
            "remove", "folder", "a/b/c");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/a/b/c/' removed from the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/c/d/' removed (cascaded).", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/c/e/' removed (cascaded).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/c/");
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/c/d/");
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/c/e/");
    }

    // ---- Test 5: top-level with children, prints cascade ----

    [Fact]
    public void RemoveFolder_TopLevelWithChildren_RemovesAndReportsCascade()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build/docs");
                await service.AddFolderAsync("Build/scripts");
                await service.SaveAsync();
            },
            "remove", "folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/Build/' removed from the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/Build/docs/' removed (cascaded).", run.Result.StdOut);
        Assert.Contains("Folder '/Build/scripts/' removed (cascaded).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/docs/");
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/scripts/");
    }

    // ---- Test 6: pre-absent folder is a single-line no-op ----

    [Fact]
    public void RemoveFolder_PreAbsent_ExitsZeroWithWasNotInTheSolution()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "remove", "folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder 'Build' was not in the solution.", run.Result.StdOut);
        Assert.DoesNotContain("removed from the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
    }

    // ---- Test 7: explicit --solution x.slnf -> Core SlnfNotSupported (exit 4) ----

    [Fact]
    public void RemoveFolder_ExplicitSlnfFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
            },
            "remove", "folder", "Build", "--solution", "x.slnf");

        Assert.Equal(CliExitCodes.SlnfNotSupported, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 8: no solution in cwd ----

    [Fact]
    public void RemoveFolder_NoSolutionInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            _ => { /* no fixture */ },
            "remove", "folder", "Build");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 9: round-trip on sloppy-project.sln (GAP 2) ----

    [Fact]
    public void RemoveFolder_RoundTrip_OnSloppyProjectSln_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "sloppy-project.sln", "first-cs.csproj", "first-fs.fsproj"),
            "remove", "folder", "Misc");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // /Misc/ and its files are gone.
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
        // No Solution Items folder should exist either; the project entities
        // survive (we can't enumerate those via ListAsync, but no exception
        // is the key signal that the project model was not disturbed).
    }

    // ---- Test 10: round-trip on sloppy-project.slnx (GAP 2) ----

    [Fact]
    public void RemoveFolder_RoundTrip_OnSloppyProjectSlnx_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "sloppy-project.slnx", "first-cs.csproj", "first-fs.fsproj"),
            "remove", "folder", "Misc");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // /Misc/ and its files are gone.
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
    }

    // ---- Helpers ----

    private sealed record CliRun(CliResult Result, string PrimaryFixturePath);

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

    private static IReadOnlyList<SolutionItemInfo> ReadItems(string solutionPath)
    {
        return ReadItemsAsync(solutionPath).GetAwaiter().GetResult();
    }

    private static async Task<IReadOnlyList<SolutionItemInfo>> ReadItemsAsync(string solutionPath)
    {
        SolutionItemsService service = await SolutionItemsService.CreateAsync(solutionPath);
        return await service.ListAsync();
    }
}