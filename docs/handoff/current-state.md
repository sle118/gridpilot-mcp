# Current state

## Project identity

The repository identity is **GridPilot MCP**.

GridPilot MCP is intended to become a local C# MCP bridge for live Excel desktop automation, focused on workbook audit, targeted refresh, Power Query diagnostics, and controlled session management.

## What currently exists

- a governance/documentation starter pack
- a C# solution skeleton using provisional `ExcelMcp.*` project names
- unit/integration/live test placeholders
- a first mock-based service example
- a first COM-backed Excel session wrapper with scoped application-state restore for `DisplayAlerts`, `EnableEvents`, and `ScreenUpdating`
- branding assets now folded into `branding/assets/`

## Important naming note

Human-facing repository materials should now use **GridPilot MCP**.

Internal code-level names remain `ExcelMcp.*` for the moment so the staged zip overlays can be applied cleanly and early implementation can proceed without a noisy rename churn. A dedicated rename pass may happen later.

## Stable direction

The intended architecture remains:

- local interactive Excel desktop automation
- out-of-process C# bridge as control plane
- workbook kept as data plane, not orchestration layer
- mock-first test strategy
- optional local-only live Excel validation tier

## Immediate gaps

- no MCP transport implementation yet
- no concrete query inventory or diagnostic probe implementation yet
- no workbook/query inventory implementation over the new session foundation yet
- no first backlog/delegation packet set yet
