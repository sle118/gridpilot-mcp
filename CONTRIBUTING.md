# Contributing

## Purpose

This repository is structured for cross-agent continuity. Any contributor, human or agent, should leave the workspace in a state where the next contributor can quickly understand what was changed, why it was changed, and what should happen next.

## Workflow expectations

Start by reading:

- `AGENTS.md`
- `docs/handoff/current-state.md`
- `docs/handoff/next-steps.md`

Then read the topic and decision documents relevant to the task.

## Documentation expectations

For any non-trivial work:

- record the session in `docs/worklog/`
- update architecture or topic documents if technical understanding changed
- update ADRs when making important design decisions
- update handoff docs when the project state or priority order changed

Narrative belongs primarily in worklogs and topic documents, not in the default repo entry points.

## Commit hygiene

Use small, reviewable commits with one intent.

Recommended commit prefixes:

- `feat:` new behavior
- `fix:` bug fix
- `refactor:` structural change without intended behavior change
- `test:` tests only or primarily tests
- `docs:` documentation only or primarily documentation
- `chore:` repository maintenance

Each meaningful commit should answer three questions:

- what changed
- why it changed
- how it was validated

## Testing model

### Unit tests

Fast, default, and required for behavior changes.

These should rely on fakes or mocks around the Excel abstraction boundary. Core orchestration logic must be testable without installed Excel.

### Integration tests

Broader service and contract tests that still avoid real Excel where possible.

### Live Excel tests

Optional workstation-only tests. These may validate real COM behavior against desktop Excel, but they must remain opt-in and excluded from normal CI.

## Live test policy

Live Excel tests should:

- require explicit opt-in
- use isolated or temporary workbook fixtures
- clean up temporary queries and artifacts on exit
- leave Excel application state restored
- never be required for GitHub-hosted validation

## Branding policy

Repository-facing identity is **GridPilot MCP**.

The current starter solution still uses provisional `ExcelMcp.*` project and namespace names. Do not rename those opportunistically. Perform a rename only through a deliberate tracked task, with tests and documentation updated together.
