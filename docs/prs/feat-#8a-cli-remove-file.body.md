Closes #42
Refs #8

## What changed

Sub-task A of parent issue #8: ships `slnx-items remove file <paths...>` as the symmetric inverse of `add file`. Same argument shape, same option flags, same resolution logic, same error table — but partitions the input into "present" vs "absent" via `ListAsync` and calls `RemoveFilesAsync` + `SaveAsync`.

- New `src/DotnetSlnxItems.Cli/Commands/RemoveFileCommand.cs` mirrors `AddFileCommand.Create()` (`public static Command Create()` + private static `RunAsync` + non-static `SetAction` lambda; `dotnet format` leaves the single-expression lambda body untouched). The verb registration lives inline in `Program.cs`, not in its own file — matches the convention already established for `add`. `remove` itself is forward-compatible with the folder-subcommand attach in #8b / #43 (one extra line directly below the `file` attach).
- `Program.cs` gains three lines directly below the existing `add` block: the `remove` verb, the `RemoveFileCommand.Create()` attach, and the root registration. No `RemoveCommand.cs` file. No `using` change.
- 8 new `[Fact]` scenarios in `tests/DotnetSlnxItems.Cli.Tests/RemoveFileCommandTests.cs` — one per acceptance-criterion row in issue #42 body. Every test asserts on `run.Result.ExitCode` via `Assert.Equal(...)` per the project's self-review checklist.

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **81/81 pass**:
  - `tests/DotnetSlnxItems.Core.Tests` 39/39 (regression baseline — unchanged by this PR).
  - `tests/DotnetSlnxItems.Cli.Tests` 42/42 (34 pre-existing + 8 new in `RemoveFileCommandTests`).
- `dotnet format whitespace` clean after a small whitespace fix committed as `chore(cli): apply dotnet format whitespace` (the new files tripped the no-trailing-newline rule on the `}` closing braces; nothing semantic).
- The 8 new CLI scenarios are (matching the design's B1–B8 table):
  1. `Help_ShowsUsageAndOptions`
  2. `RemoveFile_ExistingFileInSln_FileDisappears`
  3. `RemoveFile_ExistingFileInSlnx_FileDisappears`
  4. `RemoveFile_MultipleFilesEachRemoved`
  5. `RemoveFile_WithSolutionFolder_RemovesFromNamedFolderOnly`
  6. `RemoveFile_PreAbsentFile_ExitsZeroWithWasNotInTheSolution`
  7. `RemoveFile_ExplicitSlnfFile_NonZeroExitAndStderrExplains`
  8. `RemoveFile_OnlySlnfInCwd_NonZeroExitAndStderrExplains`
- **Apply-phase mutation sanity check (T7) PASSED.** Temporarily mutated test 6 (`RemoveFile_PreAbsentFile_ExitsZeroWithWasNotInTheSolution`) from `Assert.Equal(0, run.Result.ExitCode)` to `Assert.Equal(99, run.Result.ExitCode)`. The test failed with `Expected: 99, Actual: 0`. The mutation was reverted; the test passes again. The numeric exit-code assertion catches a wrong-exit regression cleanly — the orchestrator's pre-merge mandatory mutation gate is satisfied. **The mutation commit does NOT exist in the history** — only reverted in the working tree, per the project's mutation sanity convention.

## Line count — `size:exception` data point

- **`git diff --shortstat main..feat/#8a-cli-remove-file`** (after the `chore:` whitespace commit): 5 files changed, 511 insertions(+), 4 deletions(-).
- Per-file breakdown:
  - `src/DotnetSlnxItems.Cli/Commands/RemoveFileCommand.cs` — 156
  - `src/DotnetSlnxItems.Cli/Program.cs` — 4
  - `tests/DotnetSlnxItems.Cli.Tests/RemoveFileCommandTests.cs` — 344
  - `docs/prs/feat-#8a-cli-remove-file.body.md` — body file (this file)
- The design's review budget for MVP is 400 lines; the actual diff is **511 insertions** (overshoot of 111 lines). The `size:exception` was approved by the maintainer during the SDD explore phase per project ledger (explore observation #1071, rev 2 onwards) and locked in the proposal observation #1075 as a pre-approved decision. This PR body surfaces the actual number for the reviewer.

## Reviewer notes

1. **Design rationale (cite explore #1071).** Approach 1 ("CLI-only partition, verb inline in Program.cs") was selected over Approach 2 ("introduce `FileRemoveResult` in Core") because the issue #42 scope is explicitly "no Core change" — the cascade refactor for `FolderRemoveResult` is the only legitimate Core change for issue #8, and #8b / #43 is where it lands. Adding `FileRemoveResult` for files in #8a would dilute that and trigger an extra PublicAPI surface churn (`PublicAPI.Unshipped.txt` edit + new Core tests + a Core PR). See explore observation #1071 §Recommendation for the full tradeoff.
2. **Two distinct `.slnf` paths in tests 7 and 8 — both mandatory.** Test 7 (`RemoveFile_ExplicitSlnfFile_NonZeroExitAndStderrExplains`) exercises Core's `SlnfNotSupported` exception (exit 4) when the user passes an explicit `--solution x.slnf`. Test 8 (`RemoveFile_OnlySlnfInCwd_NonZeroExitAndStderrExplains`) exercises the CLI pre-flight via `SolutionResolver.ResolveFromCwd` returning `null` because `.slnf` is ignored (exit 2 with the CLI-owned "No solution file found in current directory" message). These are different code paths; dropping one would create a coverage hole. Explore #1071 rev 2 caught this and resolved Q1 to 8 tests. Matches parent #8 AC count + `AddFileCommandTests` precedent (which has both code paths covered too).
3. **Duplicated `ToRelativeFilePath` helper (intentional).** `RemoveFileCommand.cs` lines 147-155 (`ToRelativeFilePath` private static) are byte-for-byte identical to `AddFileCommand.cs` lines 147-155. Proposal #1075 decision #2 accepts one duplication; the third copy is the trigger to extract to `Paths/RelativePathResolver.cs` (likely when `remove folder` or `list` lands). Drift risk is real and documented in the helper's XML comment: "Kept in sync with Core's implementation; if Core changes its relative storage rules, this must change too."
4. **Idempotency contract — no summary line.** Per the issue #42 body and proposal #1075, removal prints exactly one line per input (`"File '<rel>' removed from the solution."` or `"File '<rel>' was not in the solution."`) and no summary line. The CLI calls `RemoveFilesAsync(allInputPaths, folder)` + `SaveAsync()` unconditionally — Core's `RemoveFilesAsync` silently no-ops on already-absent paths (Core line 268: `if (folder.Files is not null && ContainsFile(...))`). Test 6 is the explicit guard for the no-summary contract; it asserts on the single "was not in" line and on `Assert.DoesNotContain("removed from the solution", ...)`.
5. **No `.slnx` parity test for `--solution-folder`.** Test 5 is `.sln`-only. Mirrors the `add file` test 7 precedent (which is also `.sln`-only for the folder option) — flagged for follow-up parity sweep in #8b, not blocking for #8a.
6. **No `Co-Authored-By:` line.** Conventional commits only: commit 1 `test(cli): add RED remove file command scenarios`; commit 2 `feat(cli): wire remove file subcommand to RemoveFilesAsync`; commit 3 `chore(cli): apply dotnet format whitespace`. English-only commit bodies, no `chore:` was strictly needed (the dotnet format diff was small but real).
7. **PublicApiAnalyzers exempt.** The CLI is exempt from PublicApiAnalyzers; this PR touches only CLI files, so the Core-only analyzer does not apply. No `PublicAPI.Unshipped.txt` edit.
8. **Closes #42** (the CLI sub-issue of issue #8). Does **NOT** close #8 — the parent issue tracks the two-PR split (#8a this PR + #8b / #43 folder) and stays open until both sub-issues merge; GitHub's sub-issues feature may auto-close it after both PRs land, and the maintainer reopens as needed.
