Closes #6

## What changed

- Wires the first CLI command, `slnx-items add file <paths...>`, on top of the shipped `SolutionItemsService`. The command accepts `--solution/-s` and `--solution-folder` and resolves the solution from cwd when `--solution` is absent (single `.sln`/`.slnx` wins; multiple files in cwd surface as Core's own `AmbiguousSolutionInDirectory` error; `.slnf` is ignored).
- Adds a `Paths/SolutionResolver` and an `Errors/ErrorMapper` so the CLI's exit codes and stderr strings map exactly to the spec's "Error mapping" table (1, 2, 3, 4, 5, 6, 7 per `SolutionItemsErrorKind`).
- Lands the test surface: a `TestHelpers/CliRunner` that walks up to the `DotnetSlnxItems.slnx` marker to locate the built apphost and runs the CLI in a fresh temp cwd per test, plus 16 xUnit `[Fact]` methods — one per spec scenario plus the eight error-mapping rows — that exercise both `.sln` and `.slnx` fixtures end-to-end.

## How it was verified

- `dotnet build` clean (0 warnings, 0 errors; `TreatWarningsAsErrors=true`).
- `dotnet test` — **50/50 pass**:
  - `tests/DotnetSlnxItems.Core.Tests` 33/33 (regression baseline)
  - `tests/DotnetSlnxItems.Cli.Tests` 17/17 (16 `AddFileCommandTests` + 1 `CliRunnerSanityTests.Sanity_Run_WithVersionFlag_ReturnsZeroAndPrintsVersion`)
- The 16 CLI scenarios are:
  1. `Help_ShowsUsageAndOptions`
  2. `AddFile_SingleFile_AppendsToSolution`
  3. `AddFile_SingleFile_AppendsToSlnx`
  4. `AddFile_MultipleFilesAllNew_AppendsAllToSolution`
  5. `AddFile_MultipleFilesMixedAlreadyAndNew_AddsOnlyNew`
  6. `AddFile_AlreadyPresent_ExitsZeroWithZeroCount`
  7. `AddFile_WithSolutionFolder_PlacesFileInNamedFolder`
  8. `AddFile_NonexistentFile_NonZeroExitAndStderrExplains`
  9. `AddFile_FileOutsideSolutionDir_NonZeroExitAndStderrExplains`
  10. `AddFile_NoSolutionInCwd_NonZeroExitAndStderrExplains`
  11. `AddFile_MultipleSolutionsInCwd_NonZeroExitAndStderrExplains`
  12. `AddFile_OnlySlnfInCwd_NonZeroExitAndStderrExplains`
  13. `AddFile_ExplicitSlnfFile_NonZeroExitAndStderrExplains`
  14. `AddFile_WithExplicitSolutionPath_AddsToThatSolution`
  15. `AddFile_InvalidSolutionFolder_NonZeroExitAndStderrExplains`
  16. `AddFile_SolutionPathDoesNotExist_NonZeroExitAndStderrExplains`
- `dotnet format whitespace` applied; the resulting diff lands in its own `chore` commit.

## Reviewer notes

1. **`AGENTS.md` "Project layout" naming.** The on-disk folders are `DotnetSlnxItems.*` (matches the actual source/assembly names), but the `AGENTS.md` section "Project layout" still shows `DotnetSlnItems.*`. Fixing that text is deferred to a follow-up docs PR (out of scope for #6, which is locked to `add file`).
2. **`feat/#30-solution-items-tests` branch.** This PR was cut from `main` after PR #33 (which merged issue #30) was merged, so the Core test infrastructure it introduced is part of the base. The CLI tests build on the same `SolutionItemsService` the Core tests already exercise.
3. **PR size.** Forecast was ~680 lines; actual diff is 997 insertions + 15 deletions across 19 files. The forecast was right that the substantive logic fits in one PR; the extra lines come from the `CliRunner` discovery helper (~160 lines, all justified for self-locating the apphost across `Debug`/`Release` x `net8.0`/`net10.0` builds), the hand-written `.sln`/`.slnx`/`.csproj` fixtures (~25 lines), and the per-spec-scenario `[Fact]` body (~459 lines for 16 cases with their setup/teardown). The substantive implementation logic is closer to the forecast. Splitting would have forced two stacked PRs and cost more review time than the marginal benefit; per the maintainer's `size:exception`, the single-PR choice stands.
4. **`System.CommandLine` package.** Added `System.CommandLine 2.0.12` as a `PackageReference`. The proposal claimed it ships in the .NET 8 SDK shared framework, but it does not — it is a regular NuGet package and app code must reference it. The package version is pinned in `Directory.Packages.props` per the repo's central package management convention.
5. **Culture-invariant help output.** `Program.Main` sets `CultureInfo.DefaultThreadCurrentUICulture = InvariantCulture` before `InvokeAsync` so System.CommandLine's help copy is always English. The CLI's user-facing strings are English-only per spec, and this avoids host-locale flakiness in the substring assertions.
6. **TDD trail.** RED scaffold committed first (T5) with all 16 `[Fact]` methods failing against the placeholder `Program.cs`. Subsequent `feat(cli)` commits flipped them green in three waves (T6 → #1; T7 → #10/#11/#12/#13/#16 plus #8 from the pre-flight; T8 → all remaining). One follow-up fix commit (`fix(cli): override System.CommandLine's default --version output`) restored the placeholder's "slnx-items 0.1.0" version string that System.CommandLine's auto-`--version` would otherwise have replaced with the assembly informational version.