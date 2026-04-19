# AGENTS.md

## Project identity

This repository is **GridPilot MCP**.

GridPilot MCP is a local C# MCP bridge for live Microsoft Excel desktop automation. It exists to let agents operate Excel through a controlled host instead of embedding orchestration, diagnostics, retries, and transport logic in workbooks or VBA.

The starter implementation projects still use provisional `ExcelMcp.*` assembly and namespace names. Do not perform a broad rename unless the active task explicitly includes that migration.

## Core constraints

- Local interactive desktop Excel only
- No unattended or server-style Office automation
- The bridge owns session state safety, cleanup, retries, and diagnostics
- VBA is a temporary bootstrap/fallback path, not the intended control plane
- Prefer targeted refresh over `RefreshAll`

## Start here

1. Read `docs/handoff/current-state.md`
2. Read `docs/handoff/next-steps.md`
3. Read the relevant ADRs in `docs/decisions/`
4. Read the topic doc related to your task
5. Inspect existing tests before changing behavior

## Documentation rules

- Put durable design in `docs/architecture/`
- Put decisions in `docs/decisions/`
- Put focused technical analysis in `docs/topics/`
- Put chronological session notes in `docs/worklog/`
- Update handoff docs when project direction or priorities materially change
- Keep narrative below the surface; default entry points must stay concise

## Coding rules

- Prefer narrow, composable abstractions
- Keep raw Excel COM details isolated from the rest of the codebase
- Centralize application state save/restore
- Make cleanup idempotent
- Return structured diagnostics
- Do not introduce workbook-side orchestration unless explicitly required

## Testing rules

- Add or update unit tests for all behavior changes
- Prefer mock-based surface tests for core orchestration logic
- Keep live Excel tests optional and excluded from normal repo validation
- Never require live Excel for ordinary CI or GitHub validation
- Live tests may assume a developer workstation with Excel installed, but must remain opt-in

## Commit hygiene

- Make small commits with one clear intent
- Use conventional prefixes such as `feat:`, `fix:`, `refactor:`, `test:`, `docs:`
- Update tests and docs in the same commit when behavior or design changes
- Do not mix unrelated cleanup with functional changes
- A commit should make it easy for the next agent to understand what changed and why

## Key paths

- `branding/assets/`: repository brand assets
- `docs/handoff/`: current state and next actions
- `docs/worklog/`: dated work session records
- `src/`: starter implementation projects
- `tests/unit` and `tests/ExcelMcp.UnitTests/`: fast mock-based tests
- `tests/integration` and `tests/ExcelMcp.IntegrationTests/`: broader bridge tests
- `tests/live` and `tests/ExcelMcp.LiveTests/`: optional live Excel tests

## Taking over work

Before coding, add or update a dated worklog entry.

If you materially change architecture, priorities, or repo workflow, also update:

- `docs/handoff/current-state.md`
- `docs/handoff/next-steps.md`
- the relevant ADR or topic doc
