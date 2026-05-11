# Public Distribution And Release Workflow

This note describes how GridPilot MCP is published for users outside the local GitLab development workflow.

## Source Of Truth

- GitLab remains the working remote for development.
- GitHub is the public mirror and release surface.
- Public release tags use the `vX.Y.Z` form.

## What The Release ZIP Contains

The portable Windows release ZIP is built from source and includes:

- `README.md`
- `.env.example`
- `docs/topics/mcp-setup-and-troubleshooting.md`
- `docs/topics/public-distribution-and-release-workflow.md`
- `host/` publish output for `ExcelMcp.ToolHost`
- `proxy/` publish output for `ExcelMcp.ToolProxy`
- `tray/` publish output for `GridPilot.Tray`
- `release-manifest.json`

The package is meant to be usable on another Windows workstation without relying on tracked build outputs in the repository.

## Release Flow

1. A tagged commit matching `vX.Y.Z` triggers the GitLab release pipeline.
2. The pipeline runs the repo tests.
3. The pipeline builds the portable Windows ZIP from the current source tree.
4. The pipeline pushes the current branch and tag to GitHub so the public mirror stays aligned.
5. The pipeline creates or updates the GitHub Release and uploads the ZIP asset.

The implementation lives in:

- `.gitlab-ci.yml`
- `scripts/release/build-release-package.ps1`
- `scripts/release/publish-github-release.ps1`

## Using The Public GitHub Mirror

From another computer:

- open the GitHub repository
- download the latest `gridpilot-mcp-vX.Y.Z-windows-x64.zip` release asset
- unpack it and read `README.md`
- follow `docs/topics/mcp-setup-and-troubleshooting.md` for host registration or tray launch

## Cloning And Building Locally

If you want source instead of a release archive:

```powershell
git clone <github-mirror-url>
cd gridpilot-mcp
dotnet build ExcelMcp.sln -c Release
```

The build output stays under the project `bin/Release` folders and remains separate from the release ZIP.

## Maintainer Notes

- use the release pack script for local packaging checks
- use the publish script when mirroring tags and uploading GitHub Releases from CI
- keep compiled artifacts out of version control
