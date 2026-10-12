using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items add folder</c>
/// command (issue #7). Each test copies the minimal single-project
/// fixtures into a fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "folder already present" precondition can
/// pre-create the folder via
/// <see cref="SolutionItemsService.AddFolderAsync"/>.
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
public class AddFolderCommandTests : IDisposable
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
            "add", "folder", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("<name>", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
    }

    // ---- Test 2 + 3: top-level folder (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void AddFolder_TopLevel_AppendsToSolution(string fixture)
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, fixture, "single-project.csproj"),
            "add", "folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/Build/' added to the solution.", run.Result.StdOut);
        Assert.Contains("Created 1 folder(s).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
    }

    // ---- Test 4: nested path creates 3 ancestors, prints 3 lines ----

    [Fact]
    public void AddFolder_NestedPath_Appends3AncestorsAndPrints3Lines()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "folder", "a/b/c");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/a/' added to the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/' added to the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/c/' added to the solution.", run.Result.StdOut);
        Assert.Contains("Created 3 folder(s).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/c/");
    }

    // ---- Test 5: nested path re-run, 3 "already exists" lines ----

    [Fact]
    public void AddFolder_NestedPath_AlreadyExists_Prints3AlreadyExists()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("a/b/c");
                await service.SaveAsync();
            },
            "add", "folder", "a/b/c");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/a/' already exists in the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/' already exists in the solution.", run.Result.StdOut);
        Assert.Contains("Folder '/a/b/c/' already exists in the solution.", run.Result.StdOut);
        Assert.DoesNotContain("added to the solution", run.Result.StdOut);
        Assert.Contains("Created 0 folder(s).", run.Result.StdOut);
    }

    // ---- Test 6: top-level already exists ----

    [Fact]
    public void AddFolder_TopLevel_AlreadyExists_ExitsZero()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.SaveAsync();
            },
            "add", "folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Folder '/Build/' already exists in the solution.", run.Result.StdOut);
        Assert.DoesNotContain("added to the solution", run.Result.StdOut);
        Assert.Contains("Created 0 folder(s).", run.Result.StdOut);
    }

    // ---- Test 7: empty folder name -> Core's InvalidFolderPath ----

    [Fact]
    public void AddFolder_EmptyName_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "folder", "");

        Assert.Equal(6, run.Result.ExitCode);
        Assert.Contains(
            "Invalid folder name: ''. Folder names cannot be empty or contain only slashes.",
            run.Result.Stderr);
    }

    // ---- Test 8: only-slashes folder name -> CLI pre-flight (starts with /) ----

    [Fact]
    public void AddFolder_OnlySlashesName_NonZeroExitAndStderrExplains()
    {
        // Note: "///" starts with "/", so the CLI pre-flight rejects
        // it before the service call. Core's "cannot be empty or
        // contain only slashes" message would only surface if the
        // pre-flight allowed "/" prefixes — it does not.
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "folder", "///");

        Assert.Equal(6, run.Result.ExitCode);
        Assert.Contains(
            "Invalid folder name: '///'. Folder names cannot contain '..' or be absolute paths.",
            run.Result.Stderr);
    }

    // ---- Test 9: '..' in name -> CLI pre-flight ----

    [Fact]
    public void AddFolder_DotDotInName_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "folder", "../etc");

        Assert.Equal(6, run.Result.ExitCode);
        Assert.Contains(
            "Invalid folder name: '../etc'. Folder names cannot contain '..' or be absolute paths.",
            run.Result.Stderr);
    }

    // ---- Test 10: absolute path -> CLI pre-flight ----

    [Fact]
    public void AddFolder_AbsolutePath_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "folder", "/etc");

        Assert.Equal(6, run.Result.ExitCode);
        Assert.Contains(
            "Invalid folder name: '/etc'. Folder names cannot contain '..' or be absolute paths.",
            run.Result.Stderr);
    }

    // ---- Test 11: explicit --solution to a .slnf ----

    [Fact]
    public void AddFolder_ExplicitSlnfFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
            },
            "add", "folder", "Build", "--solution", "x.slnf");

        Assert.Equal(4, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 12: no solution in cwd ----

    [Fact]
    public void AddFolder_NoSolutionInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            _ => { /* no fixture */ },
            "add", "folder", "Build");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 13: only .slnf in cwd ----

    [Fact]
    public void AddFolder_OnlySlnfInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => File.WriteAllText(Path.Combine(dir, "only.slnf"), "<solution />"),
            "add", "folder", "Build");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 14: multiple solutions in cwd ----

    [Fact]
    public void AddFolder_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "single-project.slnx", "single-project.csproj");
            },
            "add", "folder", "Build");

        Assert.Equal(5, run.Result.ExitCode);
        Assert.Contains("Multiple solution files found in directory '", run.Result.Stderr);
    }

    // ---- Test 15: --solution points to non-existent file ----

    [Fact]
    public void AddFolder_SolutionPathDoesNotExist_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            _ => { /* no fixture */ },
            "add", "folder", "Build", "--solution", "/does/not/exist/MyApp.sln");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("Solution file not found: /does/not/exist/MyApp.sln", run.Result.Stderr);
    }

    // ---- Test 16: round-trip on sloppy-project.sln (GAP 2) ----

    [Fact]
    public void AddFolder_RoundTrip_OnSloppyProjectSln_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "sloppy-project.sln", "first-cs.csproj", "first-fs.fsproj"),
            "add", "folder", "NewFolder");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // Pre-existing /Misc/ folder and its 2 files must survive.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
        // The new folder must be present.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/NewFolder/");
    }

    // ---- Test 17: round-trip on sloppy-project.slnx (GAP 2) ----

    [Fact]
    public void AddFolder_RoundTrip_OnSloppyProjectSlnx_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "sloppy-project.slnx", "first-cs.csproj", "first-fs.fsproj"),
            "add", "folder", "NewFolder");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // Pre-existing /Misc/ folder and its 2 files must survive.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
        // The new folder must be present.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/NewFolder/");
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