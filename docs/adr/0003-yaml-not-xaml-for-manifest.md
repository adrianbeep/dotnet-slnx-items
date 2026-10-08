# ADR-0003: YAML for manifest, not XAML

Date: 2026-10-08

## Status

Accepted

## Context

The `sync` command reconciles a solution against a declarative
description of which files and folders should be its Solution Items.
That description lives in a manifest file (`.slnx-items.yaml` by
default) — the declarative counterpart of the imperative
`add`/`remove` commands.

Two audiences will touch the manifest:

- **Humans editing by hand** want comments, readability, and
  forgiveness for small mistakes.
- **CI pipelines and scripts** want a deterministic, parseable
  format with a strict schema.

The format has to serve both without one breaking the other. The
approach is a human-friendly serialisation with a strict parser
subset and a separately defined schema.

## Decision

The manifest is YAML. The default filename is `.slnx-items.yaml`
in the solution's directory.

We commit to a **safe YAML subset**: block style only (no flow
like `[a, b, c]`), no anchors or aliases, no multi-document files,
and explicit quoting for ambiguous strings like `yes`/`no`.

The schema (valid fields, types, semantics) is designed in issue
#10 and consumed by the `sync` command in issue #11. This ADR only
locks the *format*; the schema can evolve without revisiting the
format choice.

Alternatives considered and rejected:

- **XAML** brings WPF/WinForms baggage, a heavier parser, and is
  foreign to most .NET developers who don't work on UI.
- **JSON** is ubiquitous and strict, but has no comments and is
  visually noisy for nested config.
- **TOML** is clean for flat key-value pairs but the manifest has
  nested structure (solution folders, files within them) and TOML
  is awkward for that. Also uncommon in the .NET ecosystem.
- **No manifest at all** leaves only the imperative commands,
  which do not scale for code review, fresh-clone bootstrap, or
  CI enforcement.

## Consequences

What becomes easier:

- Humans can write and read the manifest with comments and nested structure.
- Pipelines can generate and parse the manifest deterministically thanks to the safe subset.
- The schema is the only thing the tool validates; format correctness is delegated to the YAML parser.

What becomes harder:

- We commit to documenting the safe subset. Tools that emit YAML
  must be configured to emit block-style, no-anchor output.
- The parser must be configured to reject the unsafe subset.
- Indentation is a footgun: a two-space vs four-space mistake
  can silently change meaning. We mitigate with schema validation
  that surfaces structural errors clearly.

What we accept:

- **YAML has spec drift** between 1.1 and 1.2 (notably booleans).
  We pin to YAML 1.2 and reject 1.1-only features.
- The schema can evolve faster than the format. Adding a new
  optional field (issue #10) does not require a new ADR; changing
  the format from YAML would.
