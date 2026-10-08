# Architecture Decision Records

We use the [Nygard ADR format](http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions) — lightweight, dated, status-tracked Markdown files.

## Naming

`NNNN-short-name.md` where `NNNN` is zero-padded 4 digits and increments monotonically. Order matters: a newer ADR has a higher number.

## Status values

- **Proposed** — under discussion. Not yet in force.
- **Accepted** — in force. The decision guides the codebase today.
- **Superseded** — replaced by a newer ADR (always link the replacement).
- **Deprecated** — no longer relevant (project abandoned that direction, etc.).

## Authoring

- Use `0000-adr-template.md` as the starting point.
- Four sections: Status, Context, Decision, Consequences.
- Aim for 30-80 lines. A good ADR captures the **why**, not the how.

## Index

| Number | Title | Status |
|---|---|---|
| 0001 | Tool name and purpose | Accepted |
| 0002 | Target frameworks | Accepted |
| 0003 | YAML for manifest, not XAML | Accepted |
| 0004 | Reject `.slnf` for `add file`/`add folder` | Pending (issue #1) |
| 0005 | `Microsoft.VisualStudio.SolutionPersistence` as persistence | Pending (issue #1) |
| 0006 | CLI shape and commands | Pending (issue #1) |

(Update this table as ADRs are written.)