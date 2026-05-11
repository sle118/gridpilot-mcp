# ADR-0004: GitLab-first development with GitHub public distribution

## Status

Accepted

## Context

The repository is developed locally against a GitLab server, but the project also needs a public GitHub presence so a separate machine can find the project, download a release, or clone and build the source.

The repo should not rely on committed build outputs. Release artifacts should be generated from the source tree and published as part of the release flow.

## Decision

- Keep GitLab as the day-to-day development remote and source of truth.
- Mirror the public branch and release tags to GitHub so the public repository can be cloned and built from source.
- Publish portable Windows ZIP release assets to GitHub Releases from tagged pipelines.
- Keep compiled release artifacts out of the repository and generate them only in the release flow.

## Consequences

### Positive

- GitLab stays the working integration point for development
- GitHub becomes a public discovery and consumption channel
- separate machines can either download a release or clone the mirrored source
- release artifacts are reproducible from the repository state and CI

### Negative

- release automation now has to manage two remotes and a public API
- tag and branch mirroring must stay healthy or the GitHub mirror can drift
- the release path needs a Windows-capable runner because the tray shell and publish outputs are Windows-targeted

## Related

- `docs/topics/public-distribution-and-release-workflow.md`
- `docs/topics/mcp-setup-and-troubleshooting.md`
- `docs/handoff/current-state.md`
