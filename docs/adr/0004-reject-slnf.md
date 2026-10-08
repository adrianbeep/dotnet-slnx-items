# ADR-0004: Reject `.slnf` for `add file`/`add folder`

Date: 2026-10-08

## Status

Accepted

## Context

`.slnf` (Solution Filter) files are a JSON-based subset of a `.sln`
or `.slnx`: they list a subset of the projects from the parent
solution, used by IDEs to open a focused view of the codebase
(e.g. "open the tests-only solution", "open the docs-only solution").
Each developer can have their own `.slnf` files, and the same project
can appear in different filters for different people.

The filter is a *view*, not a source of truth. It exists to be
opened in an IDE; it is not the canonical description of which
projects and files belong to a solution. The parent `.sln` or `.slnx`
owns that role.

A secondary technical constraint: as of `dotnet/sdk#9611` (edvilme,
2026-07), the `Microsoft.VisualStudio.SolutionPersistence` library
does not serialise `.slnf` files, and a filter only references
projects that already exist in the parent. Writing to a filter
through a tool that does not have filter serialisation support is
not a coherent operation.

This ADR covers the *write* commands (`add file`, `add folder`,
`remove file`, `remove folder`, `sync`). Reading from `.slnf` for
the `list` command is a separate question.

## Decision

The tool rejects `.slnf` as input for every command that mutates
the solution:

- `add file <paths...>`
- `add folder <name>`
- `remove file <paths...>`
- `remove folder <name>`
- `sync`

When a user passes an `.slnf` to any of these commands, the tool
exits with a non-zero status and prints a clear error explaining
that `.slnf` is a view of a parent solution and that the mutation
should be performed on the parent instead. The exact wording of the
error and the exit code are owned by issue #14, which also owns
the test matrix covering `.sln`/`.slnx` accepted and `.slnf`
rejected.

`.sln` and `.slnx` remain the only valid targets for these
commands. The `list` command is out of scope for this decision;
reading from `.slnf` for `list` is reasonable and can be revisited
when that command is implemented (issue #9).

## Consequences

What becomes easier:

- The contract is unambiguous: `.sln` and `.slnx` are the only
  writable targets. Users do not have to wonder whether their
  changes are persisting to a view or to the canonical solution.
- We do not have to implement `.slnf` serialisation against a
  library that does not support it.

What becomes harder:

- A user with a `.slnf` open in their IDE who runs the tool
  against that file gets an error instead of a silent no-op. They
  have to point the tool at the parent solution explicitly.

What we accept:

- The `list` command, if it ever accepts `.slnf` as input, will
  have a different behaviour from the write commands (read vs.
  write). This is intentional and should be documented in the
  command's own help text.
