## What changed

Switched the NuGet.org push from API key authentication to **Trusted Publishing** (OIDC-based). NuGet.org is deprecating API keys for automated publishing; Trusted Publishing is the supported replacement.

- **Removed**: the `NUGET_API_KEY` secret requirement and the `--api-key ${{ secrets.NUGET_API_KEY }}` flag from the NuGet.org push step.
- **Added**: `id-token: write` permission on the job, and the `NuGet/login@v1` step which exchanges the GitHub Actions OIDC token for a short-lived NuGet credential.
- **GitHub Packages staging path is unchanged** (still uses `GITHUB_TOKEN` for the manual `workflow_dispatch` flow).

Same workflow, two targets, selected automatically by the trigger type. No API key needed for the production path.

## How it works

- On `workflow_dispatch` (manual): push to GitHub Packages (`https://nuget.pkg.github.com/...`). Uses `GITHUB_TOKEN` automatically. Staging path — package stays private to the org.
- On push of a `v*` tag: push to NuGet.org via Trusted Publishing. The GitHub Actions runner gets a short-lived OIDC token, the `NuGet/login@v1` action exchanges it for a NuGet credential, and `dotnet nuget push` uses that credential. NuGet.org validates the token against the trusted publisher configured on the package's NuGet.org page (see Setup below).

## Setup before first use

NuGet.org now requires you to register a **trusted publisher** for the package before it will accept the OIDC token. This replaces the old "create API key" step:

1. **Create the package on NuGet.org** (one-time). The simplest way: do the very first publish manually via the NuGet.org web UI ("Upload" on the package page), or by pushing a tag that triggers this workflow — the first push will create the package as unlisted, and you can claim it from the email NuGet sends to the package owner.
2. **Go to the package's Trusted Publishing page**: `https://www.nuget.org/packages/manage/dotnet-slnx-items/trustedpublishing` (or the equivalent under Package Management).
3. **Add a new trusted publisher**:
   - **Account name**: `adrianbeep` (the GitHub owner — must match)
   - **Repository**: `dotnet-slnx-items` (the GitHub repo name)
   - **Workflow filename**: `nuget-publish.yml` (must match this workflow file)
4. **Save**.

Now the OIDC token from this workflow will be accepted. No secret to manage.

## How to test (recommended order)

1. **Staging first** — run the workflow via `workflow_dispatch`. It pushes to GitHub Packages. Check `https://github.com/adrianbeep/dotnet-slnx-items/packages`. Install from there with a PAT to validate the .nupkg works.
2. **Production second** — once the trusted publisher is configured, push a tag (`git tag v0.1.1-final && git push origin v0.1.1-final`). The workflow pushes to NuGet.org via OIDC. No API key, no secret.
3. **Verify on NuGet.org** — `https://www.nuget.org/packages/dotnet-slnx-items`. Install locally: `dotnet tool install -g dotnet-slnx-items --version 0.1.1`.

## Out of scope (still)

- Cross-platform publishing (only `ubuntu-latest` runs the workflow).
- Automatic version bumping (version in `Directory.Build.props` is bumped manually per Semver policy).
- Prerelease / stable routing via tag name (currently any `v*` tag pushes to NuGet.org as a stable version — if you want `v0.2.0-alpha.1` to be a prerelease, that needs a tag-name condition).
