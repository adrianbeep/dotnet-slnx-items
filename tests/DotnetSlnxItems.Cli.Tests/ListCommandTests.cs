using DotnetSlnxItems.Cli;
using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items list</c> command
/// (issue #9). Each test copies the minimal single-project fixtures
/// into a fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "folder already present" precondition can pre-create
/// nested folders via <see cref="SolutionItemsService.AddFolderAsync"/>.
/// The Core project is also used directly to read back the items
/// post-mutation in assertion reads (none here — <c>list</c> is
/// read-only, so no round-trip).
///
/// Per-test temp directories are tracked in <see cref="_tempDirs"/>
/// and removed on disposal (best effort) — the CLI does not write
/// back to the .sln/.slnx because <c>list</c> is read-only, but the
/// cleanup pattern is kept identical to sibling test files for
/// consistency.
///
/// <para>
/// Exit code convention: every test that runs the CLI to completion
/// asserts on <c>run.Result.ExitCode</c> via <c>Assert.Equal</c>.
/// Exit codes are taken from <see cref="CliExitCodes"/>; the
/// authoritative contract lives in
/// <c>src/DotnetSlnxItems.Cli/CliExitCodes.cs</c>:
/// 1 <c>Unhandled</c> (catch-all for <c>FolderNotFound</c>,
/// <c>PersistenceError</c>, unhandled),
/// 2 <c>NoSolutionInCwd</c> (CLI pre-flight),
/// 3 <c>SolutionFileNotFound</c> (Core),
/// 4 <c>UnsupportedFormat</c> (Core),
/// 5 <c>SlnfNotSupported</c> (Core),
/// 6 <c>AmbiguousSolutionInDirectory</c> (Core),
/// 7 <c>InvalidFolderPath</c> (CLI <c>--folder</c>
/// empty/whitespace/slashes-only or Core),
/// 8 <c>InvalidFilePath</c> (Core).
/// </para>
/// </summary>
public class ListCommandTests : IDisposable
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
            "list", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("list", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
        Assert.Contains("--folder", run.Result.StdOut);
    }

    // ---- Test 2: empty solution, no items (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void List_EmptySolution_EmptyStdoutExitZero(string fixture)
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, fixture, "single-project.csproj"),
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Equal(string.Empty, run.Result.StdOut);
    }

    // ---- Test 3: one empty folder (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void List_OneEmptyFolder_PrintsOneFolderHeaderLine(string fixture)
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, fixture, "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.SaveAsync();
            },
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        string[] lines = run.Result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "/Build/" }, lines);
    }

    // ---- Test 4: one folder with one file (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void List_OneFolderOneFile_FolderHeaderPlusIndentedFile(string fixture)
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, fixture, "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFilesAsync(new[] { "Directory.Build.props" }, "Build");
                await service.SaveAsync();
            },
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        string[] lines = run.Result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "/Build/", "  Directory.Build.props" }, lines);
    }

    // ---- Test 5: multiple folders and files ----

    [Fact]
    public void List_MultipleFoldersAndFiles_TreeWithFolderHeadersAndIndentedFiles()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
                File.WriteAllText(Path.Combine(dir, "Notes.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.AddFolderAsync("Solution Items");
                await service.AddFilesAsync(new[] { "Directory.Build.props" }, "Build");
                await service.AddFilesAsync(new[] { "Notes.md" }, "Solution Items");
                await service.SaveAsync();
            },
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        string[] lines = run.Result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "/Build/", "  Directory.Build.props", "/Solution Items/", "  Notes.md" }, lines);
    }

    // ---- Test 5b: two top-level folders — regression for indent leak ----
    //
    // The previous algorithm tracked `depth` as a counter updated
    // AFTER each Folder print, so a second top-level folder
    // (depth 1) inherited the previous folder's depth (also 1)
    // and got indented by 2 spaces. Test 5 above used
    // `Assert.Contains` on substrings, which let the bug pass
    // silently because the spurious-indent line still contains the
    // expected substring `/Build/`. This test asserts full line
    // equality so the regression is caught.

    [Fact]
    public void List_TwoTopLevelFolders_BothAtColumnZero()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "A.md"), "x");
                File.WriteAllText(Path.Combine(dir, "B.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("A");
                await service.AddFolderAsync("B");
                await service.AddFilesAsync(new[] { "A.md" }, "A");
                await service.AddFilesAsync(new[] { "B.md" }, "B");
                await service.SaveAsync();
            },
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        string[] lines = run.Result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "/A/", "  A.md", "/B/", "  B.md" }, lines);
    }

    // ---- Test 6: nested folders with correct indentation ----

    [Fact]
    public void List_NestedFolders_CorrectIndentation()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "foo.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                // AddFolderAsync("docs/infra") cascades parents:
                // /docs/ then /docs/infra/. foo.md lands under /docs/infra/.
                await service.AddFolderAsync("docs/infra");
                await service.AddFilesAsync(new[] { "foo.md" }, "docs/infra");
                await service.SaveAsync();
            },
            "list");

        Assert.Equal(0, run.Result.ExitCode);
        string[] lines = run.Result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "/docs/", "  /docs/infra/", "    foo.md" }, lines);
    }

    // ---- Test 7: --folder filters to just files ----

    [Fact]
    public void List_FolderFilter_JustFilesInFolderNoHeader()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.targets"), "<Project />");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.AddFilesAsync(new[] { "Directory.Build.props", "Directory.Build.targets" }, "Build");
                await service.SaveAsync();
            },
            "list", "--folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Directory.Build.props", run.Result.StdOut);
        Assert.Contains("Directory.Build.targets", run.Result.StdOut);
        Assert.DoesNotContain("/Build/", run.Result.StdOut);
    }

    // ---- Test 8: --folder on empty folder ----

    [Fact]
    public void List_FolderFilter_EmptyFolder_EmptyStdoutExitZero()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.SaveAsync();
            },
            "list", "--folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Equal(string.Empty, run.Result.StdOut);
    }

    // ---- Test 9: --folder on non-existent folder ----

    [Fact]
    public void List_FolderFilterNonExistent_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "list", "--folder", "Build");

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Contains("was not found", run.Result.Stderr);
        Assert.Contains("/Build/", run.Result.Stderr);
    }

    // ---- Test 10: --folder empty or only-slashes (Theory) ----

    [Theory]
    [InlineData("")]
    [InlineData("///")]
    public void List_FolderFilterEmptyOrOnlySlashes_NonZeroExit(string folderValue)
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "list", "--folder", folderValue);

        Assert.Equal(CliExitCodes.InvalidFolderPath, run.Result.ExitCode);
        Assert.Contains("cannot be empty or contain only slashes", run.Result.Stderr);
    }

    // ---- Test 11: explicit --solution x.slnf -> Core SlnfNotSupported (exit 4) ----

    [Fact]
    public void List_ExplicitSlnfFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
            },
            "list", "--solution", "x.slnf");

        Assert.Equal(CliExitCodes.SlnfNotSupported, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 12: no solution in cwd ----

    [Fact]
    public void List_NoSolutionInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            _ => { /* no fixture */ },
            "list");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 13: multiple solutions in cwd ----

    [Fact]
    public void List_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                // Copy single-project.slnx alongside .sln to trigger
                // AmbiguousSolutionInDirectory in Core.
                string sourceSlnx = Path.Combine(AppContext.BaseDirectory, "Fixtures", "single-project.slnx");
                string targetSlnx = Path.Combine(dir, "single-project.slnx");
                File.Copy(sourceSlnx, targetSlnx, overwrite: true);
            },
            "list");

        Assert.Equal(CliExitCodes.AmbiguousSolutionInDirectory, run.Result.ExitCode);
        Assert.Contains("Multiple solution files found", run.Result.Stderr);
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
}