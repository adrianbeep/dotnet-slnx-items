# ADR-0005: Microsoft.VisualStudio.SolutionPersistence as persistence

Date: 2026-10-08

## Status

Accepted

## Context

The tool reads and writes `.sln` and `.slnx` files. Both formats
have non-trivial correctness requirements: `.sln` is
whitespace-sensitive with strict GUID placement in `Project` and
`ProjectSection` blocks, and `.slnx` is an XML schema owned by
Microsoft. Neither format has a single, neutral, public
specification that third-party parsers can lock onto — both are
defined by what Visual Studio itself does.

`Microsoft.VisualStudio.SolutionPersistence` is the library Visual
Studio uses internally to read and write both formats. The dotnet/sdk
team adopted this library when they added `.slnx` support to the
SDK's own solution commands (`dotnet/sdk#44570`). The library is
open source, the API is public, and the package is on NuGet.

The decision is whether to depend on this library or to handle the
formats ourselves. The arguments in both directions are real, and
this ADR captures them so the choice can be revisited honestly later.

## Decision

The tool uses `Microsoft.VisualStudio.SolutionPersistence` for all
read and write operations on `.sln` and `.slnx` files. The
codebase never parses or writes either format by hand.

The library is wrapped behind a clean abstraction owned by issue
#2 (`SolutionItemsService`). That wrapper is the only consumer of
the library's public API; the rest of the tool goes through the
wrapper. This bounds the cost of any future library migration to
a single class.

Alternatives considered and rejected:

- **Hand-parse `.sln`**: whitespace-sensitive, unforgiving GUIDs in `Project`/`ProjectSection`, decades of accumulated edge cases. Re-implementing the parser means playing catch-up with Visual Studio forever.
- **Subprocess to `dotnet sln`**: `dotnet sln add` only accepts project files (the very gap this tool fills) and the SDK team has declined to extend it. Subprocess is also slow and gives poor errors.
- **Build on MSBuild**: it is a build engine, not a solution editor. It loads `.sln` but does not provide a clean mutation API. The underlying format is the same, so this re-introduces hand-parsing with extra weight.
- **Custom XML writer for `.slnx`**: the format is owned by Microsoft and the library exists precisely so external tools do not have to maintain a writer. The library also covers `.sln`, which a custom writer would not.

## Consequences

What becomes easier:

- Format correctness is delegated to the library VS itself uses; no re-validating whitespace, GUID placement, or XML schema.
- The same library covers both `.sln` and `.slnx` — one read/write path with a format flag.
- The `SolutionItemsService` wrapper (issue #2) gives a single place to insulate the rest of the code from library version churn.

What becomes harder:

- We depend on a Microsoft-owned package whose release cadence is tied to Visual Studio. Breaking changes are possible across major VS releases.
- The library version has to be pinned centrally and updated deliberately. A naive upgrade can pull in changes we have not validated.

What we accept:

- The library does not serialise `.slnf` files. This aligns with ADR-0004's rejection of `.slnf` for writes, but any future read-side `.slnf` support (for `list`) would need a separate path.
- Until `.slnx` stabilises in Visual Studio, we track a moving target. The library follows VS; we follow the library.
