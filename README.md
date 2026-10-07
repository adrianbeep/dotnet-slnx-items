# dotnet-slnx-items

Add non-project files and folders to `.sln`/`.slnx` solutions from the CLI.

## Status

Early development. Repo is private until a first usable release is ready.

## Why

`dotnet sln add` only accepts project files. There is no first-class way to attach `.editorconfig`, `.gitignore`, `Directory.Build.props`, `global.json`, README files or CI configs as **Solution Items** from a script. This tool fills that gap for both legacy `.sln` and the new XML-based `.slnx` format.

## Roadmap

- `add file <paths...>` and `add folder <name>`
- `remove file`, `remove folder`, `list`
- Declarative manifest with `sync`
- Optional globbing (later)