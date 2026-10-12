using DotnetSlnxItems.Cli;
using DotnetSlnxItems.Cli.Tests.TestHelpers;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// End-to-end CLI tests for the <c>slnx-items sync</c> command
/// (issue #11). Each test copies a minimal fixture set into a
/// fresh temp cwd and runs the CLI subprocess via
/// <see cref="CliRunner"/>. The Core project is referenced so tests
/// that need a "folder already present" or "file already present"
/// precondition can pre-create state via
/// <see cref="SolutionItemsService"/>.
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
/// 0 = success (created/added items, no-op, or dry-run),
/// 1 = <c>InvalidManifest</c> / <c>FileReferencedInManifestNotFound</c>
/// / <c>FolderNotFound</c> / <c>PersistenceError</c>,
/// 2 = <c>ManifestNotFound</c> or "no solution in cwd" CLI pre-flight,
/// 4 = Core <c>SlnfNotSupported</c> (when <c>--solution x.slnf</c>).
/// </para>
/// </summary>
public class SyncCommandTests : IDisposable
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
            "sync", "--help");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Usage:", run.Result.StdOut);
        Assert.Contains("sync", run.Result.StdOut);
        Assert.Contains("--manifest", run.Result.StdOut);
        Assert.Contains("--solution", run.Result.StdOut);
        Assert.Contains("--dry-run", run.Result.StdOut);
    }

    // ---- Test 2: empty manifest (zero keys) ----

    [Fact]
    public void Sync_EmptyManifest_NoOutputExitZero()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                // Truly empty manifest: no top-level keys.
                File.WriteAllText(Path.Combine(dir, ".slnx-items.yaml"), string.Empty);
            },
            "sync");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Equal(string.Empty, run.Result.StdOut);
    }

    // ---- Test 3: one folder, no files ----

    [Fact]
    public void Sync_OneFolderNoFiles_FolderCreatedExitZero()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "slnx-items-empty.yaml");
            },
            "sync", "--manifest", "slnx-items-empty.yaml");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Created folder '/Solution Items/'.", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Solution Items/");
    }

    // ---- Test 4: one folder, one file ----

    [Fact]
    public void Sync_OneFolderOneFile_FolderCreatedFileAddedExitZero()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(
                    Path.Combine(dir, ".slnx-items.yaml"),
                    "Build:\n  - Directory.Build.props\n");
            },
            "sync");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Created folder '/Build/'.", run.Result.StdOut);
        Assert.Contains("Added file 'Build/Directory.Build.props'.", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.Contains(items, i =>
            i.Kind == SolutionItemKind.File && i.Path == "Directory.Build.props");
    }

    // ---- Test 5: multiple folders and files (cross-format parity, issue #14 GAP 1) ----

    [Theory]
    [InlineData("single-project.sln")]
    [InlineData("single-project.slnx")]
    public void Sync_MultipleFoldersAndFiles_EachCreatedAndAddedSummaryCorrect(string fixture)
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, fixture, "single-project.csproj");
                // slnx-items-multi.yaml has 6 file references across
                // 5 unique files (README.md appears in two folders).
                // Pre-create every file so the pre-flight check passes.
                File.WriteAllText(Path.Combine(dir, ".editorconfig"), "root = true");
                File.WriteAllText(Path.Combine(dir, "README.md"), "# repo");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.targets"), "<Project />");
                File.WriteAllText(Path.Combine(dir, "setup.md"), "x");
                CopyFixture(dir, "slnx-items-multi.yaml");
            },
            "sync", "--manifest", "slnx-items-multi.yaml");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Created folder '/Solution Items/'.", run.Result.StdOut);
        Assert.Contains("Created folder '/Build/'.", run.Result.StdOut);
        Assert.Contains("Created folder '/docs/infrastructure/'.", run.Result.StdOut);
        Assert.Contains("Added file 'Solution Items/.editorconfig'.", run.Result.StdOut);
        Assert.Contains("Added file 'Solution Items/README.md'.", run.Result.StdOut);
        Assert.Contains("Added file 'Build/Directory.Build.props'.", run.Result.StdOut);
        Assert.Contains("Added file 'Build/Directory.Build.targets'.", run.Result.StdOut);
        Assert.Contains("Added file 'docs/infrastructure/setup.md'.", run.Result.StdOut);
        Assert.Contains("Added file 'docs/infrastructure/README.md'.", run.Result.StdOut);
        Assert.Contains("Synced 3 folders and 6 files.", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Solution Items/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/docs/infrastructure/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "Directory.Build.props");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "Directory.Build.targets");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == ".editorconfig");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "setup.md");
    }

    // ---- Test 6: idempotency — second run prints zero summary ----

    [Fact]
    public void Sync_Idempotency_SecondRunPrintsSynced0Folders0Files()
    {
        string tempDir = NewTempDir();
        CopyFixture(tempDir, "single-project.sln", "single-project.csproj");
        File.WriteAllText(Path.Combine(tempDir, ".editorconfig"), "root = true");
        File.WriteAllText(Path.Combine(tempDir, "README.md"), "# repo");
        File.WriteAllText(Path.Combine(tempDir, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(tempDir, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(tempDir, "setup.md"), "x");
        CopyFixture(tempDir, "slnx-items-multi.yaml");

        // First run: creates everything declared in the manifest.
        CliResult firstRun = CliRunner.RunWithCwd(tempDir, "sync", "--manifest", "slnx-items-multi.yaml");
        Assert.Equal(0, firstRun.ExitCode);

        // Second run: nothing to do, prints only the zero summary.
        CliResult secondRun = CliRunner.RunWithCwd(tempDir, "sync", "--manifest", "slnx-items-multi.yaml");
        Assert.Equal(0, secondRun.ExitCode);
        Assert.Contains("Synced 0 folders and 0 files.", secondRun.StdOut);
        Assert.DoesNotContain("Created folder", secondRun.StdOut);
        Assert.DoesNotContain("Added file", secondRun.StdOut);
    }

    // ---- Test 7: folder already exists, no Created line ----

    [Fact]
    public void Sync_FolderAlreadyExists_NoCreatedFolderLineOnSecondRun()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                string sln = CopyFixture(dir, "single-project.sln", "single-project.csproj");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Solution Items");
                await service.SaveAsync();
                CopyFixture(dir, "slnx-items-empty.yaml");
            },
            "sync", "--manifest", "slnx-items-empty.yaml");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.DoesNotContain("Created folder", run.Result.StdOut);
        Assert.Contains("Synced 0 folders and 0 files.", run.Result.StdOut);
    }

    // ---- Test 8: file already exists, no Added line ----

    [Fact]
    public void Sync_FileAlreadyExists_NoAddedFileLineOnSecondRun()
    {
        CliRun run = RunInTempDir(
            async dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                string sln = Path.Combine(dir, "single-project.sln");
                SolutionItemsService service = await SolutionItemsService.CreateAsync(sln);
                await service.AddFolderAsync("Build");
                await service.AddFilesAsync(new[] { "Directory.Build.props" }, "Build");
                await service.SaveAsync();
                File.WriteAllText(
                    Path.Combine(dir, ".slnx-items.yaml"),
                    "Build:\n  - Directory.Build.props\n");
            },
            "sync");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.DoesNotContain("Added file", run.Result.StdOut);
        Assert.Contains("Synced 0 folders and 0 files.", run.Result.StdOut);
    }

    // ---- Test 9: dry-run leaves solution unchanged on disk ----

    [Fact]
    public void Sync_DryRun_SolutionUnchangedOnDisk()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "slnx-items-empty.yaml");
            },
            "sync", "--manifest", "slnx-items-empty.yaml", "--dry-run");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("[DRY-RUN] Would create folder '/Solution Items/'.", run.Result.StdOut);
        Assert.Contains("[DRY-RUN] Would sync 1 folder and 0 files.", run.Result.StdOut);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        Assert.DoesNotContain(items, i =>
            i.Kind == SolutionItemKind.Folder && i.Path == "/Solution Items/");
    }

    // ---- Test 10: custom --manifest path ----

    [Fact]
    public void Sync_CustomManifestPath_AppliesTheSpecifiedManifest()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                // Default .slnx-items.yaml is intentionally absent —
                // the custom --manifest path must be honoured.
                File.WriteAllText(
                    Path.Combine(dir, "custom.yaml"),
                    "Build:\n");
            },
            "sync", "--manifest", "custom.yaml");

        Assert.Equal(0, run.Result.ExitCode);
        Assert.Contains("Created folder '/Build/'.", run.Result.StdOut);
    }

    // ---- Test 11: manifest file does not exist ----

    [Fact]
    public void Sync_ManifestFileDoesNotExist_NonZeroExitStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir => CopyFixture(dir, "single-project.sln", "single-project.csproj"),
            "sync", "--manifest", "does-not-exist.yaml");

        Assert.Equal(CliExitCodes.SolutionFileNotFound, run.Result.ExitCode);
        Assert.Contains("Manifest file not found", run.Result.Stderr);
    }

    // ---- Test 12: invalid YAML ----

    [Fact]
    public void Sync_InvalidYaml_NonZeroExitStderrHasParseErrorInfo()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "slnx-items-bad-yaml.yaml");
            },
            "sync", "--manifest", "slnx-items-bad-yaml.yaml");

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Contains("Invalid manifest", run.Result.Stderr);
    }

    // ---- Test 13: file referenced in manifest does not exist on disk ----

    [Fact]
    public void Sync_FileNotOnDisk_NonZeroExitStderrNamesFile()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(
                    Path.Combine(dir, ".slnx-items.yaml"),
                    "Build:\n  - ghost.csproj\n");
            },
            "sync");

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Contains("ghost.csproj", run.Result.Stderr);
    }

    // ---- Test 14: absolute file path in manifest ----

    [Fact]
    public void Sync_AbsolutePathInManifest_NonZeroExitStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                CopyFixture(dir, "slnx-items-absolute-path.yaml");
            },
            "sync", "--manifest", "slnx-items-absolute-path.yaml");

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Contains("Invalid manifest", run.Result.Stderr);
    }

    // ---- Test 15: explicit --solution x.slnf ----

    [Fact]
    public void Sync_SlnfSolution_NonZeroExitStderrExplains()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "single-project.sln", "single-project.csproj");
                File.WriteAllText(Path.Combine(dir, "x.slnf"), "<solution />");
                File.WriteAllText(
                    Path.Combine(dir, "manifest.yaml"),
                    "Build:\n");
            },
            "sync", "--manifest", "manifest.yaml", "--solution", "x.slnf");

        Assert.Equal(CliExitCodes.SlnfNotSupported, run.Result.ExitCode);
        Assert.Contains(".slnf", run.Result.Stderr);
    }

    // ---- Test 16: round-trip on sloppy-project.sln (GAP 2) ----

    [Fact]
    public void Sync_RoundTrip_OnSloppyProjectSln_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "sloppy-project.sln", "first-cs.csproj", "first-fs.fsproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(
                    Path.Combine(dir, ".slnx-items.yaml"),
                    "Build:\n  - Directory.Build.props\n");
            },
            "sync");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // Pre-existing /Misc/ folder and its 2 files must survive.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
        // The synced folder + file must also be present.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "Directory.Build.props");
    }

    // ---- Test 17: round-trip on sloppy-project.slnx (GAP 2) ----

    [Fact]
    public void Sync_RoundTrip_OnSloppyProjectSlnx_PreservesUnrelatedContent()
    {
        CliRun run = RunInTempDir(
            dir =>
            {
                CopyFixture(dir, "sloppy-project.slnx", "first-cs.csproj", "first-fs.fsproj");
                File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />");
                File.WriteAllText(
                    Path.Combine(dir, ".slnx-items.yaml"),
                    "Build:\n  - Directory.Build.props\n");
            },
            "sync");

        Assert.Equal(0, run.Result.ExitCode);

        IReadOnlyList<SolutionItemInfo> items = ReadItems(run.PrimaryFixturePath);
        // Pre-existing /Misc/ folder and its 2 files must survive.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Misc/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "readme-in-misc.md");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "license-in-misc.md");
        // The synced folder + file must also be present.
        Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "Directory.Build.props");
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