Closes #36

## What changed

Splits the overloaded exit code 2 and exposes the CLI's exit-code mapping through a named, documented contract. Before this change, exit code 2 was used in three semantically distinct places (CLI "no solution in cwd" pre-flight, Core `SolutionFileNotFound`, Core `ManifestNotFound` from sync), and every exit-code literal lived as a bare integer inside `ErrorMapper.cs` switch arms — unreachable from tests or external scripts.

- New `src/DotnetSlnxItems.Cli/CliExitCodes.cs`: `public static class CliExitCodes` with `public const int` members for each documented exit code. The class is the public, single-source-of-truth contract; scripts and CI pipelines should reference these constants instead of bare numbers.
- `src/DotnetSlnxItems.Cli/Errors/ErrorMapper.cs`: each switch arm now returns a `CliExitCodes.*` constant instead of a literal. The class summary is updated to reference the new contract; the default arm returns `CliExitCodes.Unhandled`.
- The CLI's inline pre-flight checks (NOT routed through `ErrorMapper`) in all 6 command files (`AddFileCommand.cs`, `RemoveFileCommand.cs`, `AddFolderCommand.cs`, `RemoveFolderCommand.cs`, `ListCommand.cs`, `SyncCommand.cs`) now use `CliExitCodes` constants. Comments referencing bare numbers are updated to reference the named constants.
- `tests/DotnetSlnxItems.Cli.Tests/*.Tests.cs` (6 files): exit-code assertions that changed value in the renumbering now assert `Assert.Equal(CliExitCodes.X, ...)` instead of `Assert.Equal(N, ...)`. Each test file also gains a `using DotnetSlnxItems.Cli;` so the constants resolve.
- All 6 test class `<summary>` blocks rewritten to enumerate the full 8-code table from `CliExitCodes` instead of duplicating the partial table from `ErrorMapper`.

### The 8-code compact table (per the locked decision in explore #1122)

| Code | Constant | Source |
|---|---|---|
| 1 | `Unhandled` | CLI pre-flight file-not-found + catch-all (`FolderNotFound`, `PersistenceError`, `InvalidManifest`, `FileReferencedInManifestNotFound`, unhandled) |
| 2 | `NoSolutionInCwd` | CLI pre-flight (no `.sln`/`.slnx` in cwd) |
| 3 | `SolutionFileNotFound` | Core (and `ManifestNotFound` for sync — both are "file/resource not found" semantics; share by analogy) |
| 4 | `UnsupportedFormat` | Core |
| 5 | `SlnfNotSupported` | Core |
| 6 | `AmbiguousSolutionInDirectory` | Core |
| 7 | `InvalidFolderPath` | Core (empty/whitespace/slashes-only) and CLI pre-flight for `..`/absolute paths in folder names |
| 8 | `InvalidFilePath` | Core (path outside the solution dir) |

The CLI pre-flight for `..` and absolute folder names in `AddFolderCommand.cs` and `RemoveFolderCommand.cs` now shares `CliExitCodes.InvalidFolderPath` (code 7) with Core's empty/whitespace/slashes-only check. The original author's inline comment in `AddFolderCommand.cs` already treated the two as semantically equivalent ("both map to exit 6 via ErrorMapper" — pre-refactor numbering); the new constant name preserves that intent.

### No Core changes

`SolutionItemsErrorKind` is unchanged. `ErrorMapper` is the only mapping site that moves. `PublicAPI.Unshipped.txt` is unchanged (CLI is exempt from `PublicApiAnalyzers`; verified — no `PublicAPI.{Shipped,Unshipped}.txt` exists under `src/DotnetSlnxItems.Cli/`).

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **159/159 pass**:
  - `tests/DotnetSlnxItems.Core.Tests` 46/46 (unchanged; no Core changes).
  - `tests/DotnetSlnxItems.Cli.Tests` 113/113 (all 113 pre-existing tests still green after the renumbering).
- `dotnet format whitespace --verify-no-changes` clean.

## Reviewer notes

1. **Line count.** Forecast from explore #1122 was ~150-200 insertions; actual is **~190 insertions** across 14 files (1 new + 13 modified + PR body file). No `size:exception` needed; well under the 400-line review budget.
2. **The split is the point.** Issue #36's body flags code 2 as overloaded (CLI pre-flight, Core `SolutionFileNotFound`, and Core `ManifestNotFound` from sync via PR #54). The split assigns each tenant a distinct, documented constant. The sync test `Sync_ManifestFileDoesNotExist_NonZeroExitStderrExplains` is the test that catches the split — it now asserts `CliExitCodes.SolutionFileNotFound` (3) instead of `ManifestNotFound` (2).
3. **The `..`/`/` pre-flight shares code 7.** `AddFolderCommand.cs:54-61` and `RemoveFolderCommand.cs:59-66` previously returned literal 6; now they return `CliExitCodes.InvalidFolderPath` (7). The author's inline comment already treated both failure modes as "invalid folder name"; the new constant name preserves that intent. Cite explore #1122 §Q2.
4. **CLI is exempt from `PublicApiAnalyzers`.** Verified by glob — no `PublicAPI.{Shipped,Unshipped}.txt` under `src/DotnetSlnxItems.Cli/`. The new `public static class CliExitCodes` is added without any analyzer entry. Core's `SolutionItemsErrorKind` is unchanged.
5. **Atomic contract change.** Pre-1.0 (per `AGENTS.md` versioning section: "MVP v0.1.0 is the first release. Pre-1.0 means anything can change"). The CLI has no external consumers yet (no NuGet release, no CI pipelines, no documented scripts keying off the codes), so the renumbering is cheap.
6. **SDD artifact updates** (separate from this PR, via `mem_update` in the apply phase). The following 7 Engram observations were updated to reflect the new exit-code table: `sdd/add-file-command/spec`, `sdd/add-file-command/design`, `sdd/add-file-command/tasks`, `sdd/sync-command/spec`, `sdd/sync-command/design`, `sdd/sync-command/tasks`, `sdd/remove-folder-command/explore`. The apply-progress observation for this change is saved under `topic_key: sdd/cli-exit-codes-refactor/apply-progress`.
7. **Conventional commits only; no `Co-Authored-By:` line.** Commit history:
   - `feat(cli): add public CliExitCodes class with named exit code constants`
   - `refactor(cli): route ErrorMapper through CliExitCodes constants`
   - `refactor(cli): align inline CLI exit codes with new table`
   - `test(cli): update exit code assertions to CliExitCodes constants`
   - `docs(cli): update exit code tables in test class summaries`
   - `docs(pr): draft PR body for chore-#36-cli-exit-codes-refactor`
8. **Closes #36 ONLY.** No parent issue; no `Refs`. Issue #36 itself is the parent of the `cli-exit-codes-refactor` capability. Memory #1084 lock.