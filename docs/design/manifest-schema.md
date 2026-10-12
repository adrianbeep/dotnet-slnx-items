# Manifest Schema — `.slnx-items.yaml`

> Schema for the declarative manifest consumed by `slnx-items sync`. Locks the *format* (ADR 0003) and the *schema* (this document) before implementation lands in the follow-up `CLI: sync` issue.

## Overview

The manifest is a single YAML file in the repository that declares the **desired set of folders and files** in the solution. `slnx-items sync` reads the file and applies the state to the `.sln` or `.slnx` solution: it creates any folder that does not exist, and adds any file that is not already in its target folder. The manifest is **additive only** — it never removes items from the solution.

The format is YAML 1.2, restricted to a **safe subset**: block style only, no anchors or aliases, no flow-style sequences, no multi-document files. See [ADR 0003](../../adr/0003-yaml-not-xaml-for-manifest.md) for the format decision.

The schema is intentionally minimal for the first version. The list of file paths under each folder is the only data structure. Per-folder metadata, globbing, and a schema version field are post-MVP.

## File location and name

The default manifest is a single file named **`.slnx-items.yaml`** at the **root of the repository** (next to `.sln` / `.slnx`). The CLI also accepts `--manifest <path>` to point at a different location or filename. The `--manifest` flag enables per-solution manifests in repositories with multiple solutions (e.g., `.slnx-items.app.yaml`, `.slnx-items.api.yaml`).

When `--manifest` is not specified, `sync` looks for `.slnx-items.yaml` in the current working directory.

## Schema

The manifest's top-level keys are **folder names**. The value of each key is a **list of file paths** to place under that folder. Folder names use `/` as a separator for nesting. File paths are relative to the manifest's location.

```yaml
"Solution Items":
  - .editorconfig
  - README.md
  - .gitignore

Build:
  - Directory.Build.props
  - Directory.Build.targets
  - Directory.Packages.props

docs/infrastructure:
  - setup.md
  - README.md
```

### Folder name rules

| Input | Maps to in solution | Example |
|---|---|---|
| Top-level name (no `/`) | `/{name}/` | `Build` → `/Build/` |
| Nested name (with `/`) | Splits into a chain of folders | `docs/infrastructure` → `/docs/` + `/docs/infrastructure/` |
| Leading or trailing `/` | Normalized away (stripped) | `/Build/` → `Build` |
| Empty string | Error: "folder name cannot be empty" | — |
| Name with `..` or absolute path | Error: "folder name cannot contain `..` or be absolute" | — |

The conventional `Solution Items` folder (with a space) is written unquoted in YAML 1.2 because spaces in bare keys are valid. Quotes are allowed when the user wants to be explicit, e.g., `"My Folder":` or `'My Folder':`. Both forms are equivalent.

There are no reserved keys. A folder literally named `target`, `version`, or any other word that might collide with future metadata is a user-chosen folder name; the user's choice wins.

### File path rules

- File paths are **relative to the manifest file's directory** (the repo root for the default `.slnx-items.yaml`). This keeps the manifest portable alongside the code it describes.
- The CLI/service converts the relative path to the path stored in the solution (which is relative to the solution file's directory). For the default repo-root case, both are the same relative path.
- A file path that points to a non-existent file on disk is a **sync error**. The manifest is a desired-state declaration; the files must exist before `sync` runs.
- An absolute file path is an **error**. Manifests are repository-relative.
- File paths are stored in the solution under the target folder with the path relative to the solution file. Forward slashes are used; backslashes are converted.
- A file path listed in the manifest is added once per target folder. The list is parsed top to bottom.
- The same file path **may** appear under multiple folder names (or under the same folder multiple times). Both cases are no-ops at the file-add level; the file is in the target folder once.

### YAML features

| Feature | Supported | Notes |
|---|---|---|
| Block-style mappings | Yes | The only supported form. |
| Block-style sequences | Yes | Lists of file paths. |
| Comments (`#` to end of line) | Yes | Recommended for human-edited files. |
| Flow-style sequences (`[a, b]`) | No | Rejected by the parser. |
| Flow-style mappings (`{a: 1}`) | No | Rejected by the parser. |
| Anchors and aliases (`&`, `*`) | No | Rejected. Manifest is literal data. |
| Multi-document files (`---`) | No | Rejected. |
| YAML 1.1 booleans (`yes`, `no`, `on`, `off`) | No | Pinned to YAML 1.2. Booleans are not used by the schema. |
| Tags (`!!str`, etc.) | No | Rejected. |
| Directives (`%YAML`, `%TAG`) | No | Rejected. |
| Duplicate keys | Error | Schema-validator surfaces the offending key. |

The parser is configured to reject the unsafe subset with a clear error message that points to the YAML construct that triggered the rejection. Implementations may use `YamlDotNet` (a pure-managed YAML 1.2 parser) with a `TypeInspector` that disallows flow-style nodes and other unsafe constructs.

## Semantics

The `SolutionItemsService.AddFile` core method silently dedupes — listing the same file path twice within one folder is a no-op (the second `AddFile` call detects the file is already there and skips it). The manifest processor does not need to validate this; the Core enforces it. The only case the user can observe is a file appearing in **different** folders, which is real behavior:

### Same file in two different folders

If the manifest lists the same file path under two different folder names, the file is added to **both** folders. The persistence library does not deduplicate across folders. `sync` does not error; the user is responsible for this choice.

```yaml
Build:
  - Directory.Build.props
"Solution Items":
  - Directory.Build.props    # file appears under both /Build/ and /Solution Items/
```

### Sync contract (what `sync` does and does not do)

`sync` is **additive only**. It:

- Creates any folder declared in the manifest that does not exist in the solution.
- Adds any file declared in the manifest that is not already in its target folder.
- Reports what it did, one line per change, plus a summary line.
- Is **idempotent**: running `sync` twice produces no changes the second time.

`sync` does **not**:

- Remove files that exist in the solution but are not in the manifest. There is no `--prune` flag in MVP.
- Remove folders that exist in the solution but are not in the manifest.
- Modify the manifest itself.
- Reorder items in the solution.
- Touch the `.sln` / `.slnx` file outside of adding Solution Items entries.
- Validate that referenced files exist on disk beyond reporting errors for non-existent ones (no separate `validate` command in MVP).

## Error cases

| Case | Sync behavior | Exit code |
|---|---|---|
| Manifest file missing | Error: "manifest not found at `<path>`" | non-zero |
| Manifest file empty (no top-level keys) | No-op, prints "nothing to sync", exit 0 | 0 |
| Manifest with one empty folder list (`Build:` with no items) | Creates the empty folder if it does not exist, exit 0 | 0 |
| File path in manifest points to a non-existent file | Error with the offending path, exits non-zero. **No partial state saved.** | non-zero |
| Folder name has trailing/leading slashes | Normalized (stripped), equivalent to no slashes | 0 (success) |
| File path is absolute | Error: "file paths must be relative to the manifest" | non-zero |
| Folder name is empty string | Error: "folder name cannot be empty" | non-zero |
| Folder name contains `..` or is absolute | Error: "folder name cannot contain `..` or be absolute" | non-zero |
| YAML anchors/aliases used | Error with a clear message pointing to the construct, exits non-zero | non-zero |
| Manifest is not valid YAML | Error with parse error location, exits non-zero | non-zero |
| `.slnf` solution passed to `--solution` | Error: `.slnf` is not supported (see [ADR 0004](../../adr/0004-reject-slnf.md)) | 4 |

The "no partial state saved" rule is critical. `sync` either applies the entire manifest successfully or fails before making changes. If a manifest declares 10 folders and folder 7 has a missing file, none of the first 6 folders are created. This avoids leaving the solution in an inconsistent state.

## Example manifest

The full reference manifest at the repo root (`.slnx-items.yaml`) covers the most common cases:

```yaml
# .slnx-items.yaml
#
# Declares folders and files that should appear in the solution.
# This file is consumed by `slnx-items sync`.

# The conventional default folder for things VS shows at the top.
"Solution Items":
  - .editorconfig
  - .gitignore
  - README.md

# Custom top-level folder for build configuration.
Build:
  - Directory.Build.props
  - Directory.Build.targets
  - Directory.Packages.props

# Nested folder using `/` as separator.
docs/infrastructure:
  - setup.md
  - README.md
```

This is a small, realistic example. It exercises:
- Quoted folder name with a space (`"Solution Items"`)
- Top-level folder (`Build`)
- Nested folder path (`docs/infrastructure` — creates both `/docs/` and `/docs/infrastructure/`)
- File paths relative to the repo root

## Validation rules summary

The schema validator (to be implemented in the `sync` command) checks the following before applying any changes:

1. **YAML safe subset**: parser rejects flow-style, anchors, multi-document, and YAML 1.1 booleans.
2. **Top-level structure**: must be a mapping of strings to lists of strings.
3. **Folder name**: no empty, no `..`, no absolute path; trim leading/trailing slashes.
4. **File path**: no absolute path; relative to manifest directory.
5. **File existence**: every listed file must exist on disk.
6. **No partial state**: if any validation step fails, no changes are applied.

## Future work (post-MVP)

- **A `--prune` flag** on `sync` that triggers full-sync semantics: additions **and** removals. Listed under #11's "Future work" section.
- **Globbing** in file lists (e.g., `*.md`, `docs/**/*.txt`). The list is literal in MVP.
- **A `slnx-items validate` command** that checks the manifest against the solution without applying changes. Useful for CI pre-merge checks.
- **A schema version field** for forward compatibility. The schema is implicit and additive in MVP; introducing a `version: 1` field is a v0.3.0+ concern.
- **Per-folder metadata** (descriptions, owners, tooling hints).
- **Schema evolution policy**: how new optional fields are added without breaking existing manifests.

## References

- [ADR 0001 — Tool name and purpose](../../adr/0001-tool-name-and-purpose.md) — the tool's scope
- [ADR 0003 — YAML for manifest, not XAML](../../adr/0003-yaml-not-xaml-for-manifest.md) — the format decision
- [ADR 0004 — Reject `.slnf`](../../adr/0004-reject-slnf.md) — why `sync` does not accept solution filters
- [ADR 0005 — `Microsoft.VisualStudio.SolutionPersistence` as the persistence layer](../../adr/0005-vs-solutionpersistence-as-persistence.md) — how the CLI mutates solutions
- [Issue #11 — CLI: `sync`](../11) — the implementation issue that consumes this schema
