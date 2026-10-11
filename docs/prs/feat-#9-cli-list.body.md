Closes #9

## What changed

Ships `slnx-items list` as the read-only counterpart to the existing `add file`, `add folder`, `remove file`, and `remove folder` commands. Walks the flat `SolutionItemInfo` stream that `SolutionItemsService.ListAsync` produces and prints a depth-aware tree; optional `--folder X` filters to files under the named folder, omitting the folder header itself.

- New `src/DotnetSlnxItems.Cli/Commands/ListCommand.cs`: `public static class` with `Create()` mirroring `RemoveFolderCommand.Create()` byte-for-byte — `Option<string?> --solution|-s`, `Option<string?> --folder` (description per design #1099), non-static `SetAction(parseResult => RunAsync(parseResult, solution, folder))` matching the `System.CommandLine 2.0.12` lock-in from #42/#43. Private static `RunAsync` resolves the target via `SolutionResolver.ResolveFromCwd` (or explicit `--solution`), routes `SolutionItemsService.CreateAsync` through `ErrorMapper`, then branches: with `--folder X`, normalizes via the rule `"/" + X.Trim('/') + "/"` (same rule Core's `NormalizeFolderPath` uses), throws `SolutionItemsException(InvalidFolderPath)` on empty/slashes-only inputs (exit 6), walks the items looking for a matching Folder record, throws `SolutionItemsException(FolderNotFound)` if none (exit 1), and prints only the files whose immediately-preceding Folder record's `Path` equals the normalized target; without `--folder`, walks items printing both Folder and File records indented by `currentFolderDepth * 2` spaces, the depth recomputed from each Folder record's `Path.Count(c => c == '/') - 1` AFTER the folder is printed. **No `SaveAsync()`** — `list` is read-only.
- `Program.cs` gains 1 line directly below the `remove` verb registration: `root.Subcommands.Add(ListCommand.Create());`. `list` is a top-level verb alongside `add` and `remove`, not nested under either. The verb stays inline per memory #1073 — no `ListCommand.cs` wrapper beyond the subcommand factory.
- 14 new test scenarios split across one file: 12 `[Fact]` + 1 `[Theory]` with 2 `[InlineData]` (`""` and `"///"`) in `tests/DotnetSlnxItems.Cli.Tests/ListCommandTests.cs`. Every test asserts on `run.Result.ExitCode` via `Assert.Equal(...)` per the AGENTS.md self-review checklist. Helpers (`RunInTempDir` sync|async, `NewTempDir`, `CopyFixture`, `CliRun` record, `_tempDirs` disposal) cloned verbatim from `RemoveFolderCommandTests.cs:234-310`.

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **106/106 pass**:
  - `tests/DotnetSlnxItems.Core.Tests` 42/42.
  - `tests/DotnetSlnxItems.Cli.Tests` 64/64 (50 pre-existing + 14 new in `ListCommandTests`: 12 `[Fact]` scenarios + 1 `[Theory]` × 2 `[InlineData]`). Note: the apply-phase convention of counting a `[Theory]` as 1 logical test yields "13 new"; xUnit expands it to 2 scenarios at run time, so the CLI tally grows from 50 to 64.
  - Forecast from design #1099 was ~105 (42 Core + 63 CLI). Actual is 106 because the `[Theory]` × 2 `[InlineData]` produces 2 scenarios (not 1) at test-run time.
- `dotnet format whitespace --verify-no-changes` was dirty on the two new files (FINALNEWLINE), fixed via `chore(cli): apply dotnet format whitespace` (mechanical, nothing semantic).
- The 14 new scenarios are (matching design #1099 §Test Plan):
  1. `Help_ShowsUsageAndOptions`
  2. `List_EmptySolution_EmptyStdoutExitZero`
  3. `List_OneEmptyFolder_PrintsOneFolderHeaderLine`
  4. `List_OneFolderOneFile_FolderHeaderPlusIndentedFile`
  5. `List_MultipleFoldersAndFiles_TreeWithFolderHeadersAndIndentedFiles`
  6. `List_NestedFolders_CorrectIndentation`
  7. `List_FolderFilter_JustFilesInFolderNoHeader`
  8. `List_FolderFilter_EmptyFolder_EmptyStdoutExitZero`
  9. `List_FolderFilterNonExistent_NonZeroExitAndStderrExplains`
  10. `List_FolderFilterEmptyOrOnlySlashes_NonZeroExit` (`[Theory]` × 2 `[InlineData]`)
  11. `List_ExplicitSlnfFile_NonZeroExitAndStderrExplains`
  12. `List_NoSolutionInCwd_NonZeroExitAndStderrExplains`
  13. `List_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains`
- **Apply-phase mutation sanity check (T7) PASSED.** Temporarily mutated test 4 (`List_OneFolderOneFile_FolderHeaderPlusIndentedFile`) from `Assert.Equal(0, run.Result.ExitCode)` to `Assert.Equal(99, run.Result.ExitCode)`. Filtered `dotnet test --filter "FullyQualifiedName~List_OneFolderOneFile"` failed with `Expected: 99, Actual: 0`. The mutation was reverted; the test passes again. The numeric exit-code assertion catches a wrong-exit regression cleanly — the orchestrator's pre-merge mandatory mutation gate is satisfied. **The mutation commit does NOT exist in the history** — only reverted in the working tree.

## Line count — `size:exception` data point

- **`git diff --shortstat eb113dc..HEAD`** (after the `chore:` whitespace commit; PR body file itself not included yet): 3 files changed, 594 insertions(+). Slightly over the ~541 forecast from design #1099 — most of the variance is in `ListCommand.cs` (214 actual vs ~120 forecast: more detailed XML doc comments and an extra try/catch around the `--folder` branch).
- Per-file breakdown:
  - `src/DotnetSlnxItems.Cli/Commands/ListCommand.cs` — 214
  - `src/DotnetSlnxItems.Cli/Program.cs` — 2 (1 blank line after the registration)
  - `tests/DotnetSlnxItems.Cli.Tests/ListCommandTests.cs` — 378
- The design's review budget for MVP is 400 lines; the actual diff at PR-body time is **594 insertions** (overshoot of 194 lines). The `size:exception` was pre-approved by the maintainer during the SDD propose phase per observation #1087 decision #2 (single-PR choice over the 2-PR split recommended in explore #1085). Precedent: PR #45 (#42) was 511 insertions, PR #46 (#43) was 574 insertions — both pre-approved. This PR body surfaces the actual number for the reviewer.

## Reviewer notes

1. **Depth-tracking algorithm (cite design #1099 §Technical Approach).** Indentation uses `new string(' ', currentFolderDepth * 2)` for BOTH Folder and File records. The depth tracker recomputes from each Folder record's `Path.Count(c => c == '/') - 1` AFTER the folder is printed, so `/Solution Items/` (one `/`) lands at depth=0 (0 spaces) while `README.md` (its child file) lands at depth=1 (2 spaces). For nested folders: `/docs/` (0) → `/docs/infra/` (1) → `foo.md` (2) → 4 spaces indent. Validated by tests 4 and 6. The algorithm is robust to any iteration order of `ListAsync` because it recomputes depth from each Folder record independently. Folder records skip when `--folder` filters so the folder's own header never appears in the filtered output.
2. **No Core change.** The `SolutionItemInfo(string Path, SolutionItemKind Kind)` refinement is already public (`PublicAPI.Unshipped.txt` lines 4-6, `SolutionItemKind` enum lines 13-17). `ListAsync` (`SolutionItemsService.cs:324-341`) already walks `_solution.SolutionFolders` in parent-before-child order (persistence library contract verified in explore #1085). The CLI walks the same flat results and prints a tree.
3. **CLI-side `--folder` validation.** The normalization rule (`"/" + value.Trim('/') + "/"` with empty/slashes-only throw) is a 3-line expression duplicated in the CLI. Rejected the alternative of exposing a Core helper because the rule is trivial, the issue scope is pure CLI, and a Core API addition would force a `PublicAPI.Unshipped.txt` entry disproportionate to the trivia. `ErrorMapper.Map(SolutionItemsException(InvalidFolderPath))` returns exit 6 with the exception's `.Message` verbatim — identical to the message Core would emit if the rule lived there. `ErrorMapper.Map(SolutionItemsException(FolderNotFound))` returns exit 1 with the spec's issue text (`"Folder '/X/' was not found in the solution."`).
4. **Read-only command.** `list` never calls `SolutionItemsService.SaveAsync` — there is nothing to persist. The sibling `add folder` / `remove folder` commands do call `SaveAsync` after mutating; `list` skips it entirely.
5. **Top-level verb registration.** Mirrors #42/#43 sibling convention. One line below `root.Subcommands.Add(remove);` at `Program.cs:67 area`. No `ListCommand.cs` wrapper file (memory #1073 — verb stays inline). The `using DotnetSlnxItems.Cli.Commands;` directive at line 4 was already present.
6. **`SetAction` 3-arg shape.** Non-static capturing lambda `parseResult => RunAsync(parseResult, solution, folder)` — matches the `RemoveFolderCommand.cs:42` and `RemoveFileCommand.cs:48` precedent. `System.CommandLine 2.0.12` has no static 3-arg overload, so a capturing lambda is required; `dotnet format` leaves it untouched because the lambda body is a single method call.
7. **`size:exception` explanation.** 594 insertions at PR-body time vs the 400-line MVP budget. Pre-approved during design #1099 per `sdd/cli-list-command/design` and the locked precedent from #43 (obs #1087 decision #2, 700-line forecast accepted). This PR adds PR body + issue body edits as separate commits; the final insertion count after those commits lands around 644 — the body surfaces ~594 because the body file itself isn't included in `git diff` of the feature commits. The actual reviewer-facing diff is consistent with the 700-line #43 precedent.
8. **Stale-issue-body edit (separate follow-up commit).** Issue #9 references #2's pending refinement as a blocker; the refinement is already in `main` (memory #1069 pattern generalized beyond sub-issues). A follow-up commit in this branch (`docs(issue): remove stale #2 dependency from #9 body`) uses `gh issue edit --body-file` per the pattern to (1) remove the blocker sentence from the Dependencies section, (2) annotate the `## Data model change in SolutionItemsService` section to note the refinement already shipped in main, and (3) append an Implementation notes section. The local-file artifact is `docs/prs/feat-#9-cli-list.body.md` (this file) plus the per-sub-task commit; the issue body edit is a `gh` CLI action with no shell artifact.
9. **CLI command registration is 1 line, not a new file.** Mirrors #42/#43 sibling convention. `Program.cs:67 area` gains `root.Subcommands.Add(ListCommand.Create());` directly below the existing `remove.Subcommands.Add(RemoveFolderCommand.Create())` line. No `ListCommand.cs` file beyond the existing subcommand factory. No new `using` directive needed.
10. **No `Co-Authored-By:` line.** Conventional commits only: commit 1 `test(cli): add RED list command scenarios`; commit 2 `feat(cli): wire list subcommand to ListAsync`; commit 3 `feat(cli): register list as top-level subcommand`; commit 4 `chore(cli): apply dotnet format whitespace`. Subsequent commits (T5: PR body, T6: stale-body edit) follow the same conventional-commits hygiene with `docs(pr):` / `docs(issue):` types.
11. **Closes #9 only.** No parent issue to reference (issue #9 itself is the parent — `addSubIssue` exists for sub-issues of #9 in future work but #9 has no parent). Memory #1084 lock — sub-issue PRs reference only the sub-issue, and top-level PRs reference only the top-level. `Closes #9` is the sole cross-reference in this body.
