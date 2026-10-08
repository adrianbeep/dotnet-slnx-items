# ADR-0001: Tool name and purpose

Date: 2026-10-08

## Status

Accepted

## Context

`dotnet sln add` accepts only project files (`.csproj`, `.fsproj`, etc.).
It has no first-class way to attach a `.editorconfig`, `.gitignore`,
`Directory.Build.props`, `global.json`, a README, or a CI config as a
**Solution Item** from a script. The same gap exists for the new
XML-based `.slnx` format, which is shipping in .NET 10 without an
equivalent non-project primitive.

Teams that want solution-level files tracked *as part of the solution*
must hand-edit the `.sln` file (fragile, whitespace-sensitive), shell
out to an IDE (not scriptable, not on Linux, not in CI), or use
general-purpose solution-file managers (usually GUI-first and silent
on `.slnx`). None of these scales, and none round-trips cleanly
between `.sln` and `.slnx`.

The gap has been raised upstream:
[`dotnet/sdk#9611`](https://github.com/dotnet/sdk/issues/9611) has
been open since 2018. The SDK team declined to add the feature to
`dotnet sln`, citing maintenance cost and scope concerns
(marcpopMSFT, 2026-08). A former SDK team member endorsed the
alternative path: build a standalone .NET tool on top of
`Microsoft.VisualStudio.SolutionPersistence` (edvilme, 2026-08).
That tool is this project.

## Decision

We will ship `dotnet-slnx-items`, a standalone CLI that adds, removes,
and lists non-project files and folders as Solution Items in both
`.sln` and `.slnx` solutions, and that can reconcile a solution
against a declarative YAML manifest (`sync`).

The name follows the established `dotnet-<tool>` convention (alongside
`dotnet-format`, `dotnet-trace`, `dotnet-dump`): `slnx-items` reads
as "items for the new `.slnx` format" while remaining honest about
also supporting legacy `.sln`. The tool is distributed as a .NET tool
or a standalone binary, not as part of the .NET SDK.

Scope is narrow on purpose: the tool manipulates **Solution Items
only**. It does not add, remove, or modify project references. It
does not run builds, restores, or tests. It does not own the
`.slnx-items.yaml` schema as a generic config format.

## Consequences

What becomes easier:

- Teams can script their solution layout in CI and bootstrap.
- One tool covers both `.sln` and `.slnx` — no per-format scripts.
- The YAML manifest makes the desired solution layout reviewable in
  PRs instead of diffed in `.sln` whitespace.

What becomes harder:

- The tool must track both `.sln` and `.slnx` as the formats evolve,
  independently of the .NET SDK's release cadence.
- Keeping the scope narrow means saying no to adjacent features
  (project file management, refactors, migrations) that look related.

What we accept:

- We duplicate functionality the .NET SDK may eventually ship
  natively. The tool's value is that it works *now*, on the formats
  that exist *now*, and is scriptable from day one.
- The `.slnx` format is still in flight. Breaking changes in the
  format may force breaking changes in this tool.
