# ADR-0002: Target frameworks

Date: 2026-10-08

## Status

Accepted

## Context

.NET ships yearly, alternating between two support tracks: **LTS**
(3 years standard + 18 months extended) and **STS** (18 months
standard, then EOL).

The live matrix as of October 2026:

| TFM     | Track | Standard support | EOL          |
|---------|-------|------------------|--------------|
| net8.0  | LTS   | until Nov 2026   | May 2028     |
| net9.0  | STS   | until May 2026   | **May 2026** (EOL) |
| net10.0 | LTS   | until Nov 2028   | May 2030     |
| net11.0 | STS   | Nov 2026 → May 2028 | May 2028  |

Supporting an EOL or STS target commits test matrix and maintenance
to a version that will be dead within 18 months. Supporting a single
current LTS forces every consumer to upgrade their stack to use the
tool. We want the broadest reach consistent with the smallest matrix
that aligns with what Microsoft actively patches.

## Decision

We multi-target `net8.0;net10.0` for the published artifacts
(`DotnetSlnItems.Core` and `DotnetSlnItems.Cli`). The source
baseline — the lowest TFM the codebase compiles against — is
`net8.0`.

- The solution emits builds for both `net8.0` and `net10.0`.
- Source uses `net8.0` APIs only; `net9.0`/`net10.0`-exclusive APIs
  must be guarded with `#if` blocks.
- The published NuGet package contains both TFMs; consumers get the
  build that matches their project.
- `net9.0` is excluded — it has been EOL since May 2026.
- `net11.0` is excluded from the published set — it is an STS, and
  forward compatibility is validated in CI against its preview SDK
  rather than shipped as a target.

## Consequences

What becomes easier:

- Consumers on either of the two most-used .NET versions today can
  use the tool without upgrading their stack.
- The published test matrix is small (2 TFMs); CI can add preview
  SDKs for forward validation without bloating the published set.

What becomes harder:

- When `net8.0` leaves standard support and `net12.0` ships as the
  next LTS, the project must cut a new ADR to update the target set.
  This is expected, not exceptional.

What we accept:

- A small set of early adopters on `net11.0` (STS) cannot consume
  the published package directly. They can build from source against
  the `net11.0` SDK if they need to.
- `net8.0` leaves standard support in November 2026, one month after
  this ADR. We keep it in the published set through its extended
  support window (until May 2028) for consumer convenience.

Update rule: when a new LTS ships (next expected: `net12.0` in
November 2027), this ADR is superseded by a new ADR that adds the
new LTS and drops the LTS that left standard support. The published
set stays "the two most recent LTS versions"; only the specific
names change.
