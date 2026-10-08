# ADR-0006: CLI shape and commands

Date: 2026-10-08

## Status

Accepted

## Context

The tool is a CLI. Its user-facing surface — verbs, arguments, the
order of subcommands — is a contract the rest of the project and
its users depend on. Changing the shape of an existing command is
a breaking change, so the shape is locked in at the ADR level
rather than discovered incrementally.

The chosen shape is six commands: `add file <paths...>`,
`add folder <name>`, `remove file <paths...>`, `remove folder
<name>`, `list`, and `sync`. The verb-first layout mirrors
`dotnet sln` (the grammar the SDK team endorsed in
`dotnet/sdk#9611`); `file` and `folder` are explicit subcommands
so the type is declared at the command level rather than inferred
from a path.

The imperative commands (`add`/`remove`) and the declarative one
(`sync`) share state: when an `.slnx-items.yaml` manifest exists,
every successful `add` or `remove` updates the manifest in the
same operation. This keeps the manifest as the source of truth
without forcing users to edit it by hand for one-off changes. If
no manifest exists, the imperative commands simply mutate the
solution.

## Decision

The tool exposes six commands in three groups:

- **Imperative mutations** (issues #6, #7, #8): `add file`, `add folder`, `remove file`, `remove folder`.
- **Read** (issue #9): `list`.
- **Declarative reconciliation** (issue #11): `sync`.

The shape is **verb-first**. `add`, `remove`, `list`, `sync` are
top-level commands; `file` and `folder` are explicit subcommands
under `add` and `remove` so the type is declared at the command
level.

When `.slnx-items.yaml` exists in the solution directory, every
successful `add` or `remove` updates the manifest in the same
operation. When no manifest exists, the imperative commands
simply mutate the solution.

**Globbing is reserved for a future ADR.** Current commands
accept literal paths only.

## Consequences

What becomes easier:

- Users can mix imperative and declarative workflows: the manifest exists for reviewability and CI, but one-off changes do not require hand-editing the YAML.
- The contract is unambiguous: a manifest that exists is always consistent with the solution.

What becomes harder:

- The manifest update must be atomic with the solution mutation. We mitigate by detecting failures clearly and giving a clean recovery path (re-run the command).
- The manifest format must support stable key ordering so `add`/`remove` do not produce noisy diffs. The schema (issue #10) defines the order.
- The validation surface grows: every mutation validates both the solution state and the manifest schema before writing.

What we accept:

- Adding or renaming a command in the future is a breaking change. The shape is locked by this ADR; we supersede it if the surface needs to evolve.
- Globbing is not in this iteration. Users who want `add file "**/*.props"` get a literal-path error until the future globbing ADR lands.
