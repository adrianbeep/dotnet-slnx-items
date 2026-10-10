Closes #7

## What changed

- Adds the public sealed record `FolderAddResult(string Path, bool Created)` under `DotnetSlnxItems.Core`, the per-folder dedupe signal that the CLI (`add folder`, PR #2) prints one line per ancestor for. The shape is locked by the spec and mirrored off the existing `SolutionItemInfo` record.
- Changes `SolutionItemsService.AddFolderAsync` from `Task` to `Task<IReadOnlyList<FolderAddResult>>`. The implementation now walks every ancestor of the normalized input path in root-to-leaf order; for each ancestor it checks `FindFolder` first and only calls `AddFolder` when the ancestor is absent, then emits one `FolderAddResult` per ancestor with the right `Created` flag.
- Adds 5 Core tests in `tests/DotnetSlnxItems.Core.Tests/AddFolderAsyncTests.cs` covering the per-folder dedupe contract (one per spec scenario 16-20), and tightens the existing `MutationTests.AddFolderAsync_NestedPath_CreatesIntermediateFolders` to also assert the new per-record `Created` signal.

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **39/39 pass** in `tests/DotnetSlnxItems.Core.Tests`:
  - 33 baseline tests (regression baseline)
  - 4 new `[Fact]` + 1 new `[Theory]` with 2 `[InlineData]` rows in `AddFolderAsyncTests` (4 + 2 = 6 test cases)
  - 1 tightened `[Fact]` in `MutationTests.AddFolderAsync_NestedPath_CreatesIntermediateFolders`
  - Total: 33 + 6 + 0 = 39 (the user-facing forecast of "38/38" counted 5 new test methods; xUnit splits the `[Theory]` into 2 rows, hence 39).
- The 5 new Core tests are (matching the design's A1-A5 table):
  1. `AddFolderAsync_NestedPath_FreshSolution_Returns3Created`
  2. `AddFolderAsync_NestedPath_ReRun_Returns3NotCreated`
  3. `AddFolderAsync_NestedPath_MixedAncestors_ReturnsMixedCreated` (rewritten — see reviewer note #4)
  4. `AddFolderAsync_TopLevel_FreshSolution_Returns1Created`
  5. `AddFolderAsync_InvalidPath_ThrowsInvalidFolderPath` (`[Theory]` over `"   "` and `"///"`)
- `dotnet format whitespace` clean (no diff produced; nothing to commit).
- **Mutation sanity check (T7)** — temporarily mutated `AddFolderAsync_TopLevel_FreshSolution_Returns1Created` from `Assert.Single(results)` to `Assert.Equal(99, results.Count)`; the test failed with `Assert.Equal() Failure: Expected: 99, Actual: 1`; the change was reverted; the test passes again. The numeric assertion catches the regression cleanly.

## Reviewer notes

1. **Public API change (source-incompatible for any caller that named the prior `Task` return).** A `grep` of `src/` and `tests/` shows zero production-code callers of `AddFolderAsync`; only test call sites in `MutationTests.cs` (lines 131, 295, 312) and `ListAsyncTests.cs` (lines 47, 73) referenced the old signature. The list-style callers awaited and discarded the return — that compiles unchanged against `Task<IReadOnlyList<FolderAddResult>>`. The two invalid-path tests (`AddFolderAsync_EmptyPath_…` / `…_OnlySlashes_…`) await-discarded as well, so they compile-clean. The one `await` site that captured the result (`MutationTests.AddFolderAsync_NestedPath_CreatesIntermediateFolders`) was tightened in this PR to assert the new per-record dedupe contract.
2. **Sentinel sequence (T2 → T3) note.** The design calls for T2's build to fail with `RS0016` ("missing entry in `PublicAPI.Unshipped.txt`") before T3 adds the `FolderAddResult` block. In this environment the `PublicApiAnalyzers` package's `RS0016` rule is silent for newly-added public types even when both `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` are present as additional files — confirmed via 5 controlled probes (build with extra public types, missing-file probe that fires `RS0048`, multiple `MicrosoftCodeAnalysisPublicApiAnalyzersRulesVersion` settings, `AnalysisMode=All`). The T3 entry in `PublicAPI.Unshipped.txt` is therefore not "decorative" — it's the source of truth for the public API surface and matches the `SolutionItemInfo` block format byte-for-byte — but the build-time gate the design relies on does not fire here. Tracking as a known environment caveat; not blocking this PR.
3. **Two-PR split rationale.** PR #1 is the Core refactor + 5 Core tests, ~80 net lines of production change (`SolutionItemsService.cs` signature/body swap + new `FolderAddResult.cs` + 4-line `PublicAPI.Unshipped.txt` update). PR #2 (the CLI command, ~600 lines, target branch: `main` after PR #1 merges) will add `AddFolderCommand.Create()`, the `--solution`/input-validation plumbing, and 15 CLI `[Fact]`s. The orchestrator's per-PR review budget for PR #1 is comfortable at ~80 lines; PR #2 will request a `size:exception` per the design's forecast (~600 > 400) and that decision is the maintainer's call.
4. **Mixed Ancestors test deviation (spec scenario 18 → A3).** The spec's premise "only `/a/b/` already exists (not `/a/`, not `/a/b/c/`)" is not reachable through the public API: `Microsoft.VisualStudio.SolutionPersistence` 1.0.52's `SolutionModel.AddFolder("/a/b/")` auto-creates every missing ancestor in one shot, so once `/a/b/` exists `/a/` exists too. The test was rewritten to extend a deeper pre-existing tree at a new sibling leaf (`a/b/c/d` pre-added, then `a/b/c/e` requested) which still produces a 4-record mixed result `[false, false, false, true]` and exercises the per-ancestor dedupe walk with the same algorithm. The test name is kept from the spec. The production code matches the design verbatim — no deviation there.
5. **v0.1.0 not yet shipped.** No deprecation path needed for the source-incompatible return type change. The `FolderAddResult` declaration lives in `PublicAPI.Unshipped.txt` and will move to `PublicAPI.Shipped.txt` when v0.1.0 ships.
6. **PublicApiAnalyzers target Core only.** The CLI is exempt; for PR #1 every changed file is in Core, so no exemption considerations apply. PR #2 will touch the CLI; the analyzer remains Core-only.