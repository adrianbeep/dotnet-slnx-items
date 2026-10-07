# Working Conventions

This file establishes how work happens in this repository. Both the human maintainer and any AI assistant collaborate through the same flow.

## TL;DR

1. Every change starts with an **issue** on the project board, in the **Backlog** column.
2. You create a **branch** named `<type>/#<N>-<slug>` where N is the issue number.
3. You commit with **conventional commits**.
4. You open a **PR** referencing `Closes #N`.
5. The maintainer reviews and merges.
6. The card moves to **Done**.

**Nothing is ever committed directly to `main`** except the initial bootstrap (which predates this convention).

## Workflow

### Issues

- Every unit of work has an issue in the [project board](https://github.com/users/adrianbeep/projects/1).
- Issue body sections: **Context**, **Acceptance Criteria**, **Technical notes**, **Dependencies**, **Out of scope**.
- Issues are written in English.
- Issues reference related issues in the Dependencies section using `#N`.
- The issue number assigned by GitHub is the source of truth. The order of creation does not dictate the order of resolution; resolution follows dependency order.

### Project board columns

| Column | Meaning |
|---|---|
| Backlog | Defined, not started |
| In Progress | Active work, branch exists |
| In Review | PR open, awaiting review |
| Done | Merged and closed |

A card lives in exactly one column. Move it when its state changes (issue or PR state changes usually trigger the move).

### Branches

Format: `<type>/#<N>-<kebab-description>`

- `type` is one of: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`
- `N` is the issue number
- `description` is short, lowercase, dash-separated

Examples:

- `feat/#3-cli-add-file`
- `fix/#7-null-reference-in-resolver`
- `docs/#1-adr-decisions`

Branches are rebased (or merged from `main`) before opening a PR.

### Commits

Conventional Commits format. The body explains the **why**, not the how.

```
<type>(<scope>): <subject>

<body>

<footer>
```

Allowed types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `perf`.

- **No `Co-Authored-By:` line for AI assistants.** Human and AI commits look identical.
- One logical change per commit. Squash noise during PR review if needed.

### Pull requests

PR body must contain:

- `Closes #N` reference to the issue
- What changed (1 to 3 bullets)
- How it was verified (test command, manual script, or example output)
- Anything the reviewer should read more carefully or that might be questioned

**Self-review checklist before requesting review:**

- [ ] Branch is up to date with `main`
- [ ] No commits directly to `main` from this work
- [ ] `dotnet build` is clean with no warnings
- [ ] `dotnet test` passes
- [ ] `dotnet format` applied
- [ ] Diff is focused (no scope creep into the issue)
- [ ] Issue acceptance criteria all met

### Code

- All code, identifiers, comments in **English**.
- C# language, `net8.0` baseline.
- Multi-target `net8.0;net9.0;net10.0` where the published artifact benefits from it.
- Format with `dotnet format` before opening PR (or rely on `.editorconfig`).
- Lock public API surfaces with `Microsoft.CodeAnalysis.PublicApiAnalyzers` for libraries.

### Testing

- xUnit for unit tests.
- Test fixtures (`.sln`, `.slnx`) hand-written, minimal, versioned under `tests/*/Fixtures/`.
- Each issue that adds behavior includes tests for it.
- Tests run on the CI pipeline (once configured).

### Versioning

- Semver: `MAJOR.MINOR.PATCH`.
- One milestone per version on GitHub.
- `MVP v0.1.0` is the first release. Pre-1.0 means anything can change.
- Version bumps happen in the PR that introduces breaking changes.

### Documentation

- `/docs/adr/` holds Architecture Decision Records (Nygard style, see `docs/adr/0000-adr-template.md`).
- ADRs are written in English, 30 to 80 lines each.
- `/docs/design/` holds longer design artifacts (schemas, MVP scope docs, etc.).
- ADRs document decisions; design docs document scope and structure.

## Boundaries for the AI assistant

The AI assistant (whatever tool the human is using) operates within explicit boundaries. These are not suggestions; they are the contract.

### Without explicit approval, the AI may:

- Read any file in the repo
- Run tests, builds, and formatters
- Create branches
- Commit and push to branches (not `main`)
- Open PRs with `Closes #N`
- Comment on issues asking clarifying questions
- Create draft issues or update existing ones
- Move project board cards to reflect work state

### The AI must ask before:

- Committing or pushing to `main`
- Closing or merging issues or PRs
- Editing release notes or version files
- Any action with effects outside the repo (e.g., publishing to NuGet)
- Making architectural decisions not already covered by an accepted ADR
- Anything the human would reasonably want to approve first

If uncertain, ask. The cost of asking is small; the cost of an unapproved action is high.

## Local commands

Common commands for working in this repo:

```bash
# Restore dependencies
dotnet restore

# Build everything
dotnet build

# Run all tests
dotnet test

# Apply formatting
dotnet format

# Run the CLI locally during development
dotnet run --project src/DotnetSlnItems.Cli -- --help
```

## Project layout (target for MVP v0.1.0)

```
.
|-- AGENTS.md                      # this file
|-- LICENSE                        # MIT
|-- README.md                      # user-facing docs
|-- Directory.Build.props          # central MSBuild properties
|-- Directory.Packages.props       # central package versions
|-- DotnetSlnItems.sln
|-- docs/
|   |-- README.md
|   |-- adr/                       # ADRs (Nygard format)
|   |   |-- 0000-adr-template.md
|   |   |-- README.md
|   |   `-- NNNN-<name>.md
|   `-- design/                    # design artifacts
|-- src/
|   |-- DotnetSlnItems.Core/       # SolutionItemsService (issue #2)
|   `-- DotnetSlnItems.Cli/        # CLI commands (issues #4+)
`-- tests/
    |-- DotnetSlnItems.Core.Tests/
    `-- DotnetSlnItems.Cli.Tests/
```

This layout will land via the Bootstrap project structure issue (currently planned as #4 in the backlog).