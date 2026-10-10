Closes #39

## What changed

- Adds `slnx-items add folder <name>`, the second sibling of `add file` (issue #7). The new `Commands/AddFolderCommand.cs` follows the same `static Create() + private static RunAsync` split as `AddFileCommand` (the `SetAction` lambda stays single-expression so `dotnet format` does not reformat a multi-statement lambda). The command accepts `<name>` (`ArgumentArity.ExactlyOne`) and `--solution|-s`.
- CLI pre-flight rejects names containing `..` or starting with `/` before any service call, printing the CLI-owned `Invalid folder name: '<v>'. Folder names cannot contain '..' or be absolute paths.` message and exiting 6. Core's `InvalidFolderPath` (empty/whitespace/slashes-only) still maps to exit 6 via `ErrorMapper`, so all "bad input" cases share the same exit code.
- `Program.cs` gains one line directly below the existing `AddFileCommand.Create()` registration: `add.Subcommands.Add(AddFolderCommand.Create());`. No other changes.
- 15 new `[Fact]` scenarios in `tests/DotnetSlnxItems.Cli.Tests/AddFolderCommandTests.cs` — one per spec scenario 1–15. Every test asserts on `run.Result.ExitCode` via `Assert.Equal(...)` per the project's self-review checklist.

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **73/73 pass**:
  - `tests/DotnetSlnxItems.Core.Tests` 39/39 (regression baseline — unchanged by this PR).
  - `tests/DotnetSlnxItems.Cli.Tests` 34/34 (19 pre-existing + 15 new in `AddFolderCommandTests`).
- The 15 new CLI scenarios are (matching the design's B1–B15 table):
  1. `Help_ShowsUsageAndOptions`
  2. `AddFolder_TopLevel_AppendsToSln`
  3. `AddFolder_TopLevel_AppendsToSlnx`
  4. `AddFolder_NestedPath_Appends3AncestorsAndPrints3Lines`
  5. `AddFolder_NestedPath_AlreadyExists_Prints3AlreadyExists`
  6. `AddFolder_TopLevel_AlreadyExists_ExitsZero`
  7. `AddFolder_EmptyName_NonZeroExitAndStderrExplains`
  8. `AddFolder_OnlySlashesName_NonZeroExitAndStderrExplains`
  9. `AddFolder_DotDotInName_NonZeroExitAndStderrExplains`
  10. `AddFolder_AbsolutePath_NonZeroExitAndStderrExplains`
  11. `AddFolder_ExplicitSlnfFile_NonZeroExitAndStderrExplains`
  12. `AddFolder_NoSolutionInCwd_NonZeroExitAndStderrExplains`
  13. `AddFolder_OnlySlnfInCwd_NonZeroExitAndStderrExplains`
  14. `AddFolder_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains`
  15. `AddFolder_SolutionPathDoesNotExist_NonZeroExitAndStderrExplains`
- `dotnet format whitespace` clean (no diff produced; no `chore:` commit).
- **Apply-phase mutation sanity check (T13) PASSED.** Temporarily mutated `AddFolder_TopLevel_AppendsToSln` (B2) from `Assert.Equal(0, run.Result.ExitCode)` to `Assert.Equal(99, run.Result.ExitCode)`. The test failed with `Expected: 99, Actual: 0`. The change was reverted; the test passes again. The numeric exit-code assertion catches the regression cleanly — the orchestrator's pre-merge mandatory mutation gate is satisfied.

## Line count — `size:exception` data point

- **`git diff --shortstat main..feat/#7-cli-add-folder-cli`**: `3 files changed, 508 insertions(+)`
- Per-file breakdown (`git diff --numstat`):
  - `src/DotnetSlnxItems.Cli/Commands/AddFolderCommand.cs` — 115
  - `src/DotnetSlnxItems.Cli/Program.cs` — 1
  - `tests/DotnetSlnxItems.Cli.Tests/AddFolderCommandTests.cs` — 392
- The design's review budget for MVP is 400 lines; the actual diff is **508 insertions** (overshoot of 108 lines). The `size:exception` was approved by the maintainer during the SDD design phase per the project's `gentle-ai sdd-attempt` ledger; this PR body just surfaces the actual number for the reviewer.

## Reviewer notes

1. **Builds on PR #1 (Core refactor).** PR #1 introduced `FolderAddResult(string Path, bool Created)` and changed `AddFolderAsync` to return `Task<IReadOnlyList<FolderAddResult>>`; PR #1 was merged to `main` at commit `c7d0c60` (PR #40). This PR was cut from that exact state. The CLI consumes the new shape directly: the `RunAsync` body iterates `records` and prints one line per entry.
2. **`size:exception` justified by three concrete factors.** (a) 15 hand-written `[Fact]` methods with their own setup/teardown and 6 helper methods (mirrored verbatim from `AddFileCommandTests.cs`); (b) `AddFolderCommand.cs` (115 lines) mirrors `AddFileCommand.cs` (156 lines) without a shared base class — YAGNI for two sibling command suites, matches `AddFileCommand`'s exact factory shape; (c) two CLI-level input-validation tests (`..` and absolute path) sit beyond the issue's nine acceptance-criterion checkboxes, raising the count from the spec's 13 to 15 scenarios.
3. **Two distinct `InvalidFolderPath` messages, intentional.** Both map to exit 6 via `ErrorMapper`. Core's `NormalizeFolderPath` throws `Invalid folder name: '<v>'. Folder names cannot be empty or contain only slashes.` (used by tests 7, 8). The CLI pre-flight writes `Invalid folder name: '<v>'. Folder names cannot contain '..' or be absolute paths.` (used by tests 9, 10). The two messages cover disjoint failure modes; both are explicit in the spec's scenario text. **Test 8 ("only-slashes `///`") was reworked during RED→GREEN** because the CLI pre-flight's leading-`/` rule catches `///` before Core sees it — the test now asserts on the CLI pre-flight's message. The spec's loose assertion (`stderr contains "Invalid folder name: '///'"`) is still satisfied; only the detailed suffix changed to match the actually-emitted text. **Test 7 (`""`) still asserts Core's message** because `""` neither contains `..` nor starts with `/`, so the pre-flight allows it through and Core throws.
4. **No `--help` example assertion.** The design's B1 table asked for an `example "slnx-items add folder Build"` substring assertion. System.CommandLine 2.0.12 does not auto-render an examples section in `--help` output (the auto-generated help only emits `Usage:`, `Arguments:`, and `Options:` sections). Asserting on that string would have failed RED→GREEN for a non-substantive reason, so test B1 asserts on `Usage:`, `<name>`, and `--solution` — the three substrings that are reliably present. Surface as a follow-up if examples need to land later.
5. **Cross-format parity.** Top-level folder tested on both `.sln` (B2) and `.slnx` (B3). Nested path tested on `.sln` (B4) — `.slnx` parity for nested paths can be a follow-up; the spec scenario 4 does not lock the format.
6. **No `Co-Authored-By:` line.** Conventional commits only: T11 `test(cli): add RED add folder command scenarios` and T12 `feat(cli): wire add folder subcommand to AddFolderAsync`. English-only commit bodies. No `chore:` commit was needed (no `dotnet format` whitespace diff).
7. **PublicApiAnalyzers exempt.** The CLI is exempt from PublicApiAnalyzers; this PR touches only CLI files, so the Core-only analyzer does not apply.
8. **Closes #39** (the CLI sub-issue of issue #7). Does **NOT** close #7 — the parent issue tracks the two-PR split and stays open until both sub-issues merge; GitHub's sub-issues feature may auto-close it after both PRs land, and the maintainer reopens as needed.