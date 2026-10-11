using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items remove file</c>
/// command (issue #42). Each test copies the minimal single-project
/// fixtures into a fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "file already present" precondition can pre-list a
/// file via <see cref="SolutionItemsService"/>.
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
/// 6 = Core <c>InvalidFolderPath</c>,
/// 7 = Core <c>InvalidFilePath</c> (path outside the solution dir).
/// </para>
/// </summary>
public class RemoveFileCommandTests : IDisposable
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
            "remove", "file", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
        Assert.Contains("--solution-folder", run.Result.StdOut);
    }

    // ---- Test 2: remove a single file (.sln) ----

    [Fact]
    public void RemoveFile_ExistingFileInSln_FileDisappears()
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
            "remove", "file", "README.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("File 'README.md' removed from the solution.", run.Result.StdOut);
        Assert.DoesNotContain("was not in the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 3: remove a single file (.slnx) ----

    [Fact]
    public void RemoveFile_ExistingFileInSlnx_FileDisappears()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string slnx = CopyFixture(dir, "single-project.slnx", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(slnx);
                await service.AddFilesAsync(new[] { "README.md" });
                await service.SaveAsync();
            },
            "remove", "file", "README.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("File 'README.md' removed from the solution.", run.Result.StdOut);
        Assert.DoesNotContain("was not in the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "README.md");
    }

    // ---- Test 4: multiple files each removed ----

    [Fact]
    public void RemoveFile_MultipleFilesEachRemoved()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "A.md"), "x");
                File.WriteAllText(Path.Combine(dir, "B.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFilesAsync(new[] { "A.md", "B.md" });
                await service.SaveAsync();
            },
            "remove", "file", "A.md", "B.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("File 'A.md' removed from the solution.", run.Result.StdOut);
        Assert.Contains("File 'B.md' removed from the solution.", run.Result.StdOut);
        Assert.DoesNotContain("was not in the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "A.md");
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "B.md");
    }

    // ---- Test 5: --solution-folder removes from the named folder only ----

    [Fact]
    public void RemoveFile_WithSolutionFolder_RemovesFromNamedFolderOnly()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
                File.WriteAllText(Path.Combine(dir, "NOTES.md"), "x");

                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                // README.md lives in /Build/; NOTES.md stays in /Solution Items/.
                await service.AddFilesAsync(new[] { "Build/README.md", "NOTES.md" });
                await service.SaveAsync();
            },
            "remove", "file", "README.md", "--solution-folder", "Build");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("File 'README.md' removed from the solution.", run.Result.StdOut);
        Assert.DoesNotContain("NOTES.md", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "Build/README.md");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "NOTES.md");
    }

    // ---- Test 6: pre-absent file is a single-line no-op ----

    [Fact]
    public void RemoveFile_PreAbsentFile_ExitsZeroWithWasNotInTheSolution()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                // MISSING.md exists on disk but was never listed in the solution.
                File.WriteAllText(Path.Combine(dir, "MISSING.md"), "x");
            },
            "remove", "file", "MISSING.md");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("File 'MISSING.md' was not in the solution.", run.Result.StdOut);
        Assert.DoesNotContain("removed from the solution", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "MISSING.md");
    }

    // ---- Test 7: explicit --solution x.slnf -> Core SlnfNotSupported (exit 4) ----

    [Fact]
    public void RemoveFile_ExplicitSlnfFile_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "remove", "file", "README.md", "--solution", "x.slnf");

        Assert.Equal(4, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 8: only .slnf in cwd -> CLI pre-flight (exit 2) ----

    [Fact]
    public void RemoveFile_OnlySlnfInCwd_NonZeroExitAndStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                File.WriteAllText(Path.Combine(dir, "only.slnf"), "<solution />");
                File.WriteAllText(Path.Combine(dir, "README.md"), "x");
            },
            "remove", "file", "README.md");

        Assert.Equal(2, run.Result.ExitCode);
        Assert.Contains("No solution file found in current directory", run.Result.Stderr);
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
