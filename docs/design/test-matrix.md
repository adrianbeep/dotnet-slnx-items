# Test Matrix Coverage — MVP `.sln`/`.slnx` Audit (issue #14)

> Cross-format test coverage matrix for the existing MVP command set
> (`add file`, `add folder`, `remove file`, `remove folder`, `list`,
> `sync`) against the supported formats (`.sln`, `.slnx`, `.slnf`-rejected).
> Closes the 5 GAPS identified in the test-coverage audit (issue #14).

## Overview

Issue #14 is an audit + gap-fill change. Each MVP command (#6-#11) carries
its own test cases, but the cross-command test matrix is implicit. Cross-format
parity (`.sln` vs `.slnx`) is not enforced by the existing fixtures; round-trip
preservation of unrelated solution content is untested; corrupt input is
untested; `--solution-folder NonExistent` is silently auto-created by Core;
and root-level invocation (`slnx-items`, `slnx-items --help`,
`slnx-items --version`) is uncovered.

This document is the **coverage matrix** that drives the test additions in
this change. It is the single source of truth for "what test must exist for
this command × format" so that no future command lands without a cross-format
test entry, and so that no future GAP sits open for a year.

## Feature × Format Coverage Matrix

| Command        | `.sln`             | `.slnx`            | `.slnf`            | Notes |
|----------------|--------------------|--------------------|--------------------|-------|
| `add file`     | covered            | covered            | rejected           | Cross-format parity collapse (GAP 1). |
| `add folder`   | covered            | covered            | rejected           | Cross-format parity collapse (GAP 1). |
| `remove file`  | covered            | covered            | rejected           | Cross-format parity collapse (GAP 1). |
| `remove folder`| covered            | covered            | rejected           | Cross-format parity collapse (GAP 1). |
| `list`         | covered            | covered            | rejected           | Cross-format parity collapse (GAP 1). |
| `sync`         | covered            | covered            | rejected           | `Sync_MultipleFoldersAndFiles_EachCreatedAndAddedSummaryCorrect` parity conversion. |
| root (no args) | covered            | covered            | covered            | GAP 5: `Root_NoArgs`, `Root_Help`, `Root_Version` (3 `[Fact]`s). |

`.slnf` is rejected by Core at `SolutionItemsService.ValidateExtension`
(`src/DotnetSlnxItems.Core/SolutionItemsService.cs:391-409`), which throws
`SolutionItemsException(SlnfNotSupported)` → exit 4 via
`src/DotnetSlnxItems.Cli/Errors/ErrorMapper.cs:22`. The rejection message
reads: "Solution filters (.slnf) are read-only projections. Run the command
on the parent .sln or .slnx instead." This matrix does not change that
contract.

## Cross-Format Parity Dimension

Each of the 5 existing CLI test files (plus `SyncCommandTests.cs`) owns a
*paired pair* of `[Fact]`s for the same scenario — one in `.sln`, one in
`.slnx`. Those pairs collapse into a single `[Theory]` with:

```csharp
[Theory]
[InlineData("single-project.sln")]
[InlineData("single-project.slnx")]
public void AddFile_SingleFile_AppendsToSolution(string fixtureFile)
{
    // ... test body uses CopyFixture(dir, $"single-project.{ext}", ...)
}
```

`CliRunner.RunWithCwd` does not need a `Format` parameter; the format
signal is implicit in the copied fixture's extension. Tests that are
intrinsically format-specific (`.slnf` rejection, multi-solution ambiguity,
file outside solution dir) stay `[Fact]`.

The collapsed body is identical to the originals. The refactor is
mechanical: copy the `.sln` body, swap `[Fact]` for `[Theory]` +
`[InlineData]`, then thread the fixture filename through `CopyFixture`. The
ListCommandTests.cs `[Theory]` precedent at lines 282–293 and
AddFolderCommandTests.cs empty/slashes-only theories are the templates.

The collapsed `[Theory]`s cover the same matrix entries above. The
`[InlineData]` count of 2 (one per format) is the only test-count growth:
the pre-existing paired `[Fact]`s already covered both formats individually
in separate methods.

## Cross-Cutting Scenarios

| Scenario                | What it tests | Where it lives | Test count |
|-------------------------|---------------|----------------|------------|
| Round-trip preservation | A mutation does not strip or re-parent pre-existing solution content (comments, GlobalSection, PropertyGroup, multi-project entries, pre-existing `/Misc/` folder + files). | `sloppy-project.{sln,slnx}` fixture + paired `[Fact]`s in each of the 5 mutating-command test files. | 10 |
| Corrupt input behavior  | A 6 commands × 2 formats matrix where the corruption is rejected at the persistence layer with exit 1, non-empty stderr, and no `Exception:` or `at ` markers (no stack trace leak). | `corrupt.{sln,slnx}` fixtures + paired `[Fact]`s. | 12 |
| `--solution-folder` missing | An explicit `--solution-folder` against a folder that does not exist in the solution throws `FolderNotFound` → exit 1, stderr `Folder '/<normalized>/' was not found in the solution.`. | Single-project fixtures + paired `[Theory]`s × 2 `[InlineData]` rows for `add file` and `remove file`. | 4 |
| Root invocation         | `slnx-items` (no args) → exit 1 + stderr "missing subcommand"; `--help` → exit 0 + stdout lists all 4 verbs; `--version` → exit 0 + stdout "slnx-items 0.1.0". | New `RootCommandTests.cs` (3 `[Fact]`s). | 3 |
| **Cross-cutting total** | | | **+29** |

The remaining +2 new tests come from the 2 new Core `[Fact]`s for GAP 4
(`AddFilesAsync_RequireExistingFolder_ThrowsWhenFolderMissing` +
`RemoveFilesAsync_RequireExistingFolder_ThrowsWhenFolderMissing`). Net total:
**+31** new tests; **+153** total scenarios (122 pre-existing + 31 new).

## Fixtures

| File path                                        | Lines | Purpose |
|--------------------------------------------------|-------|---------|
| `tests/DotnetSlnxItems.Cli.Tests/Fixtures/sloppy-project.sln`  | ~25 | Round-trip preservation: 2 projects + `/Misc/` folder + 2 files + `GlobalSection(ExtensibilityGlobals)` block. |
| `tests/DotnetSlnxItems.Cli.Tests/Fixtures/sloppy-project.slnx` | ~15 | Round-trip preservation: 2 projects + `/Misc/` folder + 2 files + `<PropertyGroup>` block. |
| `tests/DotnetSlnxItems.Cli.Tests/Fixtures/corrupt.sln`         | ~3  | Missing the `Microsoft Visual Studio Solution File, Format Version 12.00` header line. Core throws `PersistenceError` ("Not a solution file."). |
| `tests/DotnetSlnxItems.Cli.Tests/Fixtures/corrupt.slnx`        | ~4  | Truncated mid-element (`<Project Path="first.csproj"` with no closing). Core throws `PersistenceError` with a clean XML parser message. |
| `tests/DotnetSlnxItems.Cli.Tests/Fixtures/solution-with-filters.slnf` | ~5 | Valid `.slnf` referencing `single-project.csproj`. Core rejects with `SlnfNotSupported` → exit 4. Documented shape for the `.slnf`-rejected contract. |

The `.slnx` fixture uses `<Solution xmlns="https://github.com/dotnet/slnx">`
and `<PropertyGroup>` inside one of the projects. These are the
microsoft/ext documented schema extensions; the persistence library
(`Microsoft.VisualStudio.SolutionPersistence` 1.0.52) accepts them on both
load and round-trip. Apply phase probed both before committing (T2).

The `corrupt.{sln,slnx}` fixtures throw with `PersistenceError` and a
human-readable message. The message text is short (no stack trace) so
the GAP 3 test assertion `Assert.DoesNotContain("at ", stderr)` passes
without any try/catch wrapping in the CLI.

## GAP Traceability

| GAP | What it closed | Where the test lives | Test count |
|---|---|---|---|
| GAP 1 (parity) | `[Fact]` pair → `[Theory]` × 2 `[InlineData]` rows. | `AddFileCommandTests.cs`, `AddFolderCommandTests.cs`, `RemoveFileCommandTests.cs`, `RemoveFolderCommandTests.cs`, `ListCommandTests.cs`, `SyncCommandTests.cs`. | 0 (collapse; same coverage, half the methods) |
| GAP 2 (round-trip) | 10 NEW `[Fact]`s asserting pre-existing entities survive a mutation. | 5 mutating-command test files × 2 formats. | +10 |
| GAP 3 (corrupt) | 12 NEW `[Fact]`s asserting clean exit-1 + clean stderr (no `Exception:` / no `at `). | 6 commands × 2 formats. | +12 |
| GAP 4 (`--solution-folder` missing) | Core: optional `bool requireExistingFolder = false` on `AddFilesAsync` + `RemoveFilesAsync`. CLI: pass `true` only when `--solution-folder` is explicit. Tests: 4 NEW paired `[Theory]`s + 2 NEW Core `[Fact]`s. | `AddFileCommandTests.cs`, `RemoveFileCommandTests.cs`, `MutationTests.cs`. | +6 |
| GAP 5 (root invocation) | 3 NEW `[Fact]`s: `Root_NoArgs`, `Root_Help`, `Root_Version`. | New `RootCommandTests.cs`. | +3 |
| **Total** | | | **+31** |

## GAP 4 — `--solution-folder NonExistent` Contract

The only Core change in this PR. The new optional parameter and its
behavior are documented in `src/DotnetSlnxItems.Core/SolutionItemsService.cs`:

- `AddFilesAsync(IReadOnlyList<string> filePaths, string? targetFolder = null, bool requireExistingFolder = false, CancellationToken ct = default)`.
- `RemoveFilesAsync(IReadOnlyList<string> filePaths, string? targetFolder = null, bool requireExistingFolder = false, CancellationToken ct = default)`.

When `requireExistingFolder` is `true` and the target folder does not exist
in the solution (`_solution.FindFolder(folderPath) is null`), the method
throws `SolutionItemsException(FolderNotFound, $"Folder '{folderPath}' was
not found in the solution.")`. The message format matches
`src/DotnetSlnxItems.Cli/Commands/ListCommand.cs:174` verbatim for
greppable consistency.

Default `false` preserves the existing MVP behavior:
- `AddFilesAsync` silently auto-creates the folder.
- `RemoveFilesAsync` silently no-ops on absent folders.

The CLI commands pass `true` **only** when the user explicitly passed
`--solution-folder`. The default `/Solution Items/` flow passes `false`,
unchanged. The MVP contract for `add file` (default `/Solution Items/`
auto-create on first use) is preserved.

`PublicAPI.Unshipped.txt` lines 43 and 45 gain `bool requireExistingFolder = false`
before `CancellationToken`. No new public type, no new enum value
(`FolderNotFound` already exists at `SolutionItemsErrorKind.cs:30`,
numeric value 6). No new `ErrorMapper.cs` mapping (FolderNotFound is
already routed to exit 1 at `ErrorMapper.cs:26`).

## What This Document Does NOT Cover

- **Performance benchmarks** — out of scope for issue #14 (audit + gap-fill).
- **Cross-platform OS tests** — `slnx-items` is a local file-mutation tool; the test harness is identical on Linux/macOS/Windows. There is no `RuntimeInformation`-branched behavior worth testing.
- **Coverage tooling** — no coverlet, no coverage gate. The MVP test surface is small and well-known; explicit "all paths tested" review is sufficient at this size.
- **Mutation testing** — manual mutation sanity is performed once at PR time (T16 of the apply task list) but no automatic mutation-testing framework is adopted.
- **Issue #6 / #8 contract updates** — the `--solution-folder NonExistent` rule (GAP 4) is a contract change for issues #6 and #8. Per the issue body, those issues are NOT edited from this PR; a follow-up comment on issue #14 post-merge documents the contract change and links to the relevant acceptance criteria lines.

## References

- Issue #14 — Test matrix coverage audit.
- `docs/design/manifest-schema.md` — the parallel design doc for `sync`, same table style.
- `src/DotnetSlnxItems.Core/SolutionItemsService.cs` — the Core API surface touched by GAP 4.
- `src/DotnetSlnxItems.Cli/Errors/ErrorMapper.cs:26` — the `FolderNotFound → exit 1` mapping.
