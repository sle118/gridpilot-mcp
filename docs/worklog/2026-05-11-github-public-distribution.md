# Worklog: 2026-05-11 - GitHub public distribution and release packaging

## Goal

Make GridPilot MCP easy to discover and consume from a separate computer by publishing a GitHub mirror plus portable Windows release ZIPs, while keeping GitLab as the day-to-day development remote.

## Planned changes

- add a durable repo decision for GitLab-first development and GitHub public distribution
- add a reproducible release-pack script that builds the Windows host, proxy, and tray outputs into a versioned ZIP
- add a GitLab CI release job that mirrors the main branch and tags to GitHub and publishes a GitHub Release asset
- update the README and setup docs so a new user can either download a release or clone and build locally
- refresh the handoff docs and deployment inventory so the repo workflow stays accurate

## Constraints

- do not commit compiled release artifacts
- keep the public release path portable and Windows-focused
- preserve GitLab as the working remote and GitHub as the public mirror
- keep the release package self-contained enough for a separate workstation to use without repo archaeology

## Implemented

- added `ADR-0004` for GitLab-first development with GitHub public distribution
- added a tag-driven GitLab CI release pipeline in `.gitlab-ci.yml`
- added `scripts/release/build-release-package.ps1` to produce a versioned Windows ZIP with host, proxy, tray, README, setup docs, and a release manifest
- added `scripts/release/publish-github-release.ps1` to mirror the main branch and tags to GitHub and upload the release asset
- added `docs/topics/public-distribution-and-release-workflow.md` plus README and setup-doc updates for release download and local clone/build use
- refreshed the handoff docs and deployment inventory so the public release path is now part of the documented repo workflow
- added a small packaging-naming helper and unit test coverage for the release ZIP naming contract
- pinned the GitLab CI jobs to a `windows-release` runner tag so the release pipeline can target the Windows VM we will provision on the LXD host
- hardened the GitHub publish script with explicit checks for missing CI variables after the first tagged release run exposed an empty `GITHUB_REPOSITORY_URL` in the job environment
- hardened the GitHub publish script again after the first publish retry showed a malformed tag refspec and an auth URL echoed into the job log; the script now normalizes the release tag and uses a temporary credential store for git pushes

## Validation

- `git diff --check`
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -c Release --filter ReleasePackageNamingTests`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/build-release-package.ps1 -Version v0.0.0-test -OutputRoot .tmp\\test-release-pack`
- confirmed the generated ZIP contains `host\\ExcelMcp.ToolHost.exe`, `proxy\\ExcelMcp.ToolProxy.exe`, and `tray\\GridPilot.Tray.exe`
