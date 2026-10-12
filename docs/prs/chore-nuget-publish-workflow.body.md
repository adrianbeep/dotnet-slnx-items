## What changed

A new GitHub Actions workflow at `.github/workflows/nuget-publish.yml` that publishes the CLI's NuGet package.

- **Manual dispatch (`workflow_dispatch`)** → push to **GitHub Packages** (the org-level registry at `https://nuget.pkg.github.com/adrianbeep/dotnet-slnx-items/`). Safe staging: no public visibility, no external cost. Anyone in the repo can install from this feed with a Personal Access Token. Use this to verify the package installs and runs correctly before going to NuGet.org.
- **Tag push (`v*`)** → push to **NuGet.org** (the public registry at `https://www.nuget.org/packages/dotnet-slnx-items`). This is the production path. Requires the `NUGET_API_KEY` secret (set in repo Settings → Secrets → Actions).

Same workflow, two targets, selected automatically by the trigger type. No `target` input — keeps the UI simple and prevents accidental production pushes.

## How it works

- Triggers on:
  - `push` of any tag matching `v*` (e.g., `v0.1.1`, `v1.0.0-rc.1`)
  - `workflow_dispatch` (manual run from the Actions tab)
- Job runs on `ubuntu-latest` (matrix in #58 is for tests; publishing needs no Windows runner)
- `concurrency` group with `cancel-in-progress: false` — publishing jobs should not be cancelled (would leave the registry in an inconsistent state)
- Steps:
  - `actions/checkout@v4` + `actions/setup-dotnet@v4` with .NET 8.0.x
  - `dotnet pack` builds the `.nupkg`
  - Conditional step:
    - On `workflow_dispatch` → push to GitHub Packages (uses `GITHUB_TOKEN` with `packages: write` permission, automatic)
    - On `push` of `v*` tag → push to NuGet.org (uses `NUGET_API_KEY` secret, manual setup)

## Setup before first use

1. **Create a NuGet.org account** at https://www.nuget.org/ (if you don't have one)
2. **Create an API key** at https://www.nuget.org/account/apikeys with scope `Push new packages and package versions` and package id pattern `dotnet-slnx-items` (to limit the key to this package)
3. **Add the API key as a GitHub Actions secret** at https://github.com/adrianbeep/dotnet-slnx-items/settings/secrets/actions with name `NUGET_API_KEY`

## Out of scope

- Prerelease support: the workflow always pushes to NuGet.org, regardless of whether the tag is a prerelease (e.g., `v0.2.0-alpha.1`). If you want prereleases to go only to GitHub Packages and stable to NuGet.org, add a condition based on the tag name.
- Cross-platform publishing: only `ubuntu-latest` is used (matches the current CI runner). Windows/macOS aren't needed for publishing.
- Automatic version bumping: the version in `Directory.Build.props` must be bumped manually (per AGENTS.md Semver policy). The workflow reads the current value.
