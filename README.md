# dotnet-slnx-items

> Add non-project files and folders to `.sln` and `.slnx` solutions as **Solution Items** from the CLI.

[![build + test](https://github.com/adrianbeep/dotnet-slnx-items/actions/workflows/build-test.yml/badge.svg)](https://github.com/adrianbeep/dotnet-slnx-items/actions/workflows/build-test.yml)

## Description

`dotnet sln add` only accepts project files. There is no first-class way to attach `.editorconfig`, `.gitignore`, `Directory.Build.props`, `Directory.Build.targets`, `global.json`, README files, or CI configs as **Solution Items** from a script. This tool fills that gap for both the legacy `.sln` format and the new XML-based `.slnx` format.

These items appear in Visual Studio's **Solution Items** folder in Solution Explorer — a UI concept that the .NET SDK does not currently expose to its CLI. The upstream team declined to add this feature in [dotnet/sdk#9611](https://github.com/dotnet/sdk/issues/9611), so we built a standalone CLI that wraps `Microsoft.VisualStudio.SolutionPersistence` directly.

## Status

**MVP v0.1.0** is the first usable release. The tool ships with five commands for both `.sln` and `.slnx` formats:

- `add file`, `add folder` — attach items to a solution
- `remove file`, `remove folder` — detach items
- `list` — inspect what's in the solution

Pre-1.0: the API and CLI surface may change between minor versions before 1.0. See [Limitations](#limitations) for what is and is not supported today.

## Install

Install as a global .NET tool:

```bash
dotnet tool install -g dotnet-slnx-items
```

Update an existing install:

```bash
dotnet tool update -g dotnet-slnx-items
```

Uninstall:

```bash
dotnet tool uninstall -g dotnet-slnx-items
```

The tool targets `net8.0` and `net10.0`. A .NET SDK 8.0 or later is required.

## Quickstart

From a directory containing `App.slnx` (or `App.sln`):

```bash
$ slnx-items add file .editorconfig .gitignore README.md
Added: .editorconfig
Added: .gitignore
Added: README.md
Added 3 file(s).

$ slnx-items list
/Solution Items/
  .editorconfig
  .gitignore
  README.md
```

The three files now appear in Visual Studio's **Solution Items** folder in Solution Explorer.

Add a custom virtual folder and put files in it:

```bash
$ slnx-items add folder Build
Folder '/Build/' added to the solution.
Created 1 folder(s).

$ echo "<Project />" > Directory.Build.props
$ echo "<Target />" > Directory.Build.targets
$ slnx-items add file Directory.Build.props Directory.Build.targets --solution-folder Build
Added: Directory.Build.props
Added: Directory.Build.targets
Added 2 file(s).

$ slnx-items list
/Solution Items/
  .editorconfig
  .gitignore
  README.md
/Build/
  Directory.Build.props
  Directory.Build.targets
```

`add folder` is recursive: `slnx-items add folder docs/infra` creates both `/docs/` and `/docs/infra/` in one shot.

## Commands

| Command | Purpose |
|---|---|
| [`add file <paths...>`](#slnx-items-add-file-paths) | Add one or more files to a Solution Items folder. |
| [`add folder <name>`](#slnx-items-add-folder-name) | Add a virtual folder (auto-creates parents). |
| [`remove file <paths...>`](#slnx-items-remove-file-paths) | Remove one or more files (idempotent). |
| [`remove folder <name>`](#slnx-items-remove-folder-name) | Remove a virtual folder (cascades to children). |
| [`list`](#slnx-items-list) | List all folders and their files. |
| [`sync`](#slnx-items-sync) | Apply a `.slnx-items.yaml` manifest to the solution (additive only). |

`slnx-items --help` and `slnx-items <command> --help` are the canonical full reference for every option.

### `slnx-items add file <paths...>`

Add one or more files to a Solution Items folder. Files must already exist on disk; they are referenced by their path relative to the solution directory.

```bash
slnx-items add file <paths...> [options]
```

**Options**

- `-s, --solution <path>` — Path to a `.sln` or `.slnx` file. If omitted, resolves from the current working directory (must contain exactly one solution file).
- `--solution-folder <name>` — Destination folder. Defaults to `Solution Items`. Top-level (e.g. `Build`) or nested (e.g. `docs/infrastructure`).

**Examples**

```bash
# Add three files to the default Solution Items folder
slnx-items add file .editorconfig .gitignore README.md

# Add files to a custom folder
slnx-items add file Directory.Build.props Directory.Build.targets --solution-folder Build

# Nested folder path
slnx-items add file docs/api/openapi.yaml --solution-folder docs/api
```

### `slnx-items add folder <name>`

Add a virtual folder. Intermediate folders in the path are created automatically. The folder is empty after creation; follow up with `add file` to put files in it.

```bash
slnx-items add folder <name>
```

**Examples**

```bash
# Top-level folder
slnx-items add folder Build

# Nested folder — both /docs/ and /docs/infra/ are created
slnx-items add folder docs/infra
```

Re-running the same command exits 0 and prints `Folder '<name>' already exists in the solution.` (idempotent).

### `slnx-items remove file <paths...>`

Remove one or more files from the solution. The command is **idempotent**: removing a file that is not in the solution exits 0 with a `File '<path>' was not in the solution.` message. The file is not deleted from disk.

```bash
slnx-items remove file <paths...> [options]
```

**Options** — same as `add file`.

### `slnx-items remove folder <name>`

Remove a virtual folder. The folder's children (files and nested folders) are removed in the same operation.

```bash
slnx-items remove folder <name>
```

**Examples**

```bash
# Remove a top-level folder
slnx-items remove folder Build

# Remove a nested folder
slnx-items remove folder docs/infra
```

Re-running the same command exits 0 with `Folder '<path>' was not in the solution.` (idempotent).

### `slnx-items list`

List all Solution Items as a tree. Folders are emitted first, files within each folder are indented by two spaces per nesting level.

```bash
slnx-items list [options]
```

**Options**

- `-s, --solution <path>` — Same as `add file`.
- `--folder <name>` — Show only the files under the named folder. Output is a flat list (one file path per line, no header, no indentation). The folder must exist; passing a non-existent folder exits non-zero.

**Examples**

```
$ slnx-items list
/Solution Items/
  .editorconfig
  .gitignore
  README.md
/Build/
  Directory.Build.props
  Directory.Build.targets

$ slnx-items list --folder Build
Directory.Build.props
Directory.Build.targets
```

### `slnx-items sync`

Apply a `.slnx-items.yaml` manifest to the solution. `sync` is the declarative counterpart to the `add` / `remove` commands: it reads the manifest, computes the diff against the current solution, and applies only the missing folders and files. `sync` is **additive only** — it never removes items from the solution, and there is no `--prune` flag in v0.1.0.

```bash
slnx-items sync [options]
```

**Options**

- `-m, --manifest <path>` — Path to a `.slnx-items.yaml` manifest. Defaults to `.slnx-items.yaml` in the current working directory.
- `-s, --solution <path>` — Path to a `.sln` or `.slnx` file, or a directory containing exactly one. Same resolution rules as the other commands.
- `--dry-run` — Print the changes that would be applied without modifying the solution.

**Manifest format**

A YAML file mapping folder names to lists of file paths. Folder names use `/` for nesting. **File paths are relative to the manifest's directory** (the repo root by default), not to the folder name — a path under a nested folder must repeat the prefix. See [`docs/design/manifest-schema.md`](./docs/design/manifest-schema.md) for the full schema.

```yaml
"Solution Items":
  - .editorconfig
  - .gitignore
  - README.md

Build:
  - Directory.Build.props
  - Directory.Build.targets

# Nested folder; the file paths repeat the `docs/infrastructure/`
# prefix because the path is anchored at the manifest directory.
docs/infrastructure:
  - docs/infrastructure/setup.md
  - docs/infrastructure/README.md
```

**Examples**

```bash
# Apply the default manifest in the current directory.
slnx-items sync

# Preview the changes without writing to the solution.
slnx-items sync --dry-run

# Apply a non-default manifest.
slnx-items sync --manifest .slnx-items.api.yaml
```

`sync` is idempotent: running it twice produces no changes the second time. The full schema (safe-YAML subset, folder name rules, error cases) is documented in [`docs/design/manifest-schema.md`](./docs/design/manifest-schema.md).

## Exit codes

All commands share the same exit-code table:

| Code | Meaning |
|---|---|
| 0 | Success (including idempotent no-op cases). |
| 1 | Catch-all for unexpected errors (`PersistenceError`, unhandled exceptions). |
| 2 | No solution file in cwd. |
| 3 | Explicit `--solution` path does not exist, or the `.slnx-items.yaml` manifest does not exist (sync). |
| 4 | Unsupported solution file format (neither `.sln` nor `.slnx`). |
| 5 | `.slnf` (solution filter) file rejected. |
| 6 | Multiple solution files in the directory given to `--solution` and no explicit file to disambiguate. |
| 7 | Invalid folder name (empty, whitespace, only slashes, contains `..`, or absolute path). |
| 8 | File path is outside the solution directory. |

Run `slnx-items <command> --help` for the option-level help of each command.

## Limitations

- **`.slnf` (solution filter) files are not supported.** Solution filters are read-only projections of a parent solution; this tool does not write to them. Run the command against the parent `.sln` or `.slnx` instead. Exit code 5 is returned for `.slnf` paths.
- **No shell-style globbing.** Paths are interpreted literally. Use your shell's expansion (`*.props`) to pass multiple files.
- **No `--prune` or full-sync removal.** `slnx-items sync` is additive only: it creates missing folders and adds missing files, but does not remove items already in the solution. A `--prune` flag that prunes to the manifest's declared state is post-MVP. The `remove` commands still target one file or folder at a time.
- **Pre-1.0 API surface.** The CLI and command shape may change between minor versions before 1.0. Pin your install (`dotnet tool install -g dotnet-slnx-items --version 0.1.0`) if you need reproducible builds.
- **Windows reserved names not validated.** The persistence library does not block folder names like `CON`, `PRN`, or `AUX`. Avoid these.

## Design

Architecture decisions are documented as ADRs in [`/docs/adr/`](./docs/adr/README.md):

- `0001` — Tool name and purpose
- `0002` — Target frameworks (`net8.0`, `net10.0`)
- `0003` — YAML for the manifest, not XAML
- `0004` — Reject `.slnf` for `add file` / `add folder`
- `0005` — `Microsoft.VisualStudio.SolutionPersistence` as the persistence layer
- `0006` — CLI shape and the five MVP commands

## Contributing

See [`AGENTS.md`](./AGENTS.md) at the repo root for working conventions: branch naming (`feat/#N-...`, `fix/#N-...`, `chore/#N-...`), commit format (Conventional Commits, no `Co-Authored-By`), the PR self-review checklist, and the issue-to-PR workflow.

The maintainer reviews and merges PRs. There is one milestone per Semver release on GitHub; `MVP v0.1.0` is the first.

## Testing

Run the test suite locally:

```bash
dotnet test
```

The suite has 107 xUnit tests covering the five MVP commands and the `SolutionItemsService` core library. CI runs the same suite on every PR via GitHub Actions (see the badge at the top of this README).

## License

[MIT](./LICENSE) — Copyright (c) 2026 Adrian Joaquin Naum.
