using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items add file</c> command
/// (issue #6). Each test copies the minimal single-project fixtures
/// into a fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "file already present" precondition can pre-list a
/// file via <see cref="SolutionItemsService"/>.
///
/// Per-test temp directories are tracked in <see cref="_tempDirs"/>
/// and removed on disposal (best effort) — the CLI writes back to
/// the .sln/.slnx on disk, so the test body still has read access
/// to the solution while the test runs.
/// </summary>
public class AddFileCommandTests : IDisposable
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
            "add", "file", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
        Assert.Contains("--solution-folder", run.Result.StdOut);
    }

    // ---- Test 2: add a single file (.sln) ----

    [Fact]
    public void AddFile_SingleFile_AppendsToSolution()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Added: README.md", run.Result.StdOut);
        Assert.Contains("Added 1 file(s).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 3: add a single file (.slnx) ----

    [Fact]
    public void AddFile_SingleFile_AppendsToSlnx()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.slnx", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Added: README.md", run.Result.StdOut);
        Assert.Contains("Added 1 file(s).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 4: multiple new files ----

    [Fact]
    public void AddFile_MultipleFilesAllNew_AppendsAllToSolution()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "A.md"), "x");
                File.WriteAllText(Path.Combine(dir, "B.md"), "x");
            },
            "add", "file", "A.md", "B.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Added: A.md", run.Result.StdOut);
        Assert.Contains("Added: B.md", run.Result.StdOut);
        Assert.Contains("Added 2 file(s).", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "A.md");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "B.md");
    }

    // ---- Test 5: mixed already + new ----

    [Fact]
    public void AddFile_MultipleFilesMixedAlreadyAndNew_AddsOnlyNew()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "A.md"), "x");
                File.WriteAllText(Path.Combine(dir, "B.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFilesAsync(new[] { "A.md" });
                await service.SaveAsync();
            },
            "add", "file", "A.md", "B.md");

        Assert.Contains("Added: B.md", run.Result.StdOut);
        Assert.DoesNotContain("Added: A.md", run.Result.StdOut);
        Assert.Contains("Added 1 file(s).", run.Result.StdOut);
    }

    // ---- Test 6: already present ----

    [Fact]
    public void AddFile_AlreadyPresent_ExitsZeroWithZeroCount()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFilesAsync(new[] { "README.md" });
                await service.SaveAsync();
            },
            "add", "file", "README.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Added 0 file(s).", run.Result.StdOut);
        Assert.DoesNotContain("Added: README.md", run.Result.StdOut);
    }

    // ---- Test 7: custom --solution-folder ----

    [Fact]
    public void AddFile_WithSolutionFolder_PlacesFileInNamedFolder()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md", "--solution-folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Added: README.md", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 8: file does not exist on disk ----

    [Fact]
    public void AddFile_NonexistentFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "add", "file", "MISSING.txt");

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Contains("File not found: 'MISSING.txt'", run.Result.Stderr);
    }

    // ---- Test 9: file outside solution directory ----

    [Fact]
    public void AddFile_FileOutsideSolutionDir_NonZeroExitAndStderrExplains()
    {
        var setupResult = SetupOuterDirs();
        string solutionDir = setupResult.SolutionDir;
        string cwdDir = setupResult.CwdDir;
        string outerDir = setupResult.OuterDir;

        CopyFixture(solutionDir, "single-project.sln", "single-project.csproj");

        string otherDir = Path.Combine(outerDir, "other");
        Directory.CreateDirectory(otherDir);
        string outsideFile = Path.Combine(otherDir, "file.txt");
        File.WriteAllText(outsideFile, "x");

        string solutionPath = Path.Combine(solutionDir, "single-project.sln");
        CliResult result = RunCli(cwdDir, "add", "file", outsideFile, "--solution", solutionPath);

        Assert.Equal(7, result.ExitCode);
        Assert.Contains(
            $"File path '{outsideFile}' is outside the solution directory '{solutionDir}'.",
            result.Stderr);
    }

    // ---- Test 10: no solution in cwd ----

    [Fact]
    public void AddFile_NoSolutionInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => File.WriteAllText(Path.Combine(dir, "README.md"), "x"),
            "add", "file", "README.md");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 11: multiple solutions in cwd ----

    [Fact]
    public void AddFile_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "single-project.slnx", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        Assert.Equal(5, run.Result.ExitCode);
        Assert.Contains("Multiple solution files found in directory '", run.Result.Stderr);
    }

    // ---- Test 12: only .slnf in cwd ----

    [Fact]
    public void AddFile_OnlySlnfInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                File.WriteAllText(Path.Combine(dir, "only.slnf"), "<solution />");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
    }

    // ---- Test 13: explicit --solution to a .slnf ----

    [Fact]
    public void AddFile_ExplicitSlnfFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md", "--solution", "x.slnf");

        Assert.Equal(4, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 14: --solution points to a file in another dir ----

    [Fact]
    public void AddFile_WithExplicitSolutionPath_AddsToThatSolution()
    {
        var setupResult = SetupOuterDirs();
        string solutionDir = setupResult.SolutionDir;
        string cwdDir = setupResult.CwdDir;

        CopyFixture(solutionDir, "single-project.sln", "single-project.csproj");
        File.WriteAllText(Path.Combine(cwdDir, "README.md"), "x");

        string solutionPath = Path.Combine(solutionDir, "single-project.sln");
        CliResult result = RunCli(
            cwdDir,
            "add", "file", "README.md", "--solution", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Added: README.md", result.StdOut);
        Assert.Contains("Added 1 file(s).", result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(solutionPath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 15: empty --solution-folder ----

    [Fact]
    public void AddFile_InvalidSolutionFolder_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "add", "file", "README.md", "--solution-folder", "");

        Assert.Equal(6, run.Result.ExitCode);
        Assert.Contains(
            "Invalid folder name: ''. Folder names cannot be empty or contain only slashes.",
            run.Result.Stderr);
    }

    // ---- Test 16: --solution points to a non-existent file ----

    [Fact]
    public void AddFile_SolutionPathDoesNotExist_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => File.WriteAllText(Path.Combine(dir, "README.md"), "x"),
            "add", "file", "README.md", "--solution", "/does/not/exist/MyApp.sln");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("Solution file not found: /does/not/exist/MyApp.sln", run.Result.Stderr);
    }

    // ---- Helpers ----

    private sealed record CliRun(CliResult Result, string PrimaryFixturePath);

    private sealed record OuterDirs(string OuterDir, string SolutionDir, string CwdDir);

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

    private OuterDirs SetupOuterDirs()
    {
        string outerDir = NewTempDir();
        string solutionDir = Path.Combine(outerDir, "proj");
        string cwdDir = Path.Combine(outerDir, "cwd");
        Directory.CreateDirectory(solutionDir);
        Directory.CreateDirectory(cwdDir);
        return new OuterDirs(outerDir, solutionDir, cwdDir);
    }

    private CliResult RunCli(string cwd, params string[] args)
    {
        try
        {
            return CliRunner.RunWithCwd(cwd, args);
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

    private string CopyFixture(string targetDir, params string[] names)
    {
        string sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        string firstTarget = string.Empty;
        foreach (string name in names)
        {
            string source = Path.Combine(sourceDir, name);
            string target = Path.Combine(targetDir, name);
            File.Copy(source, target);
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