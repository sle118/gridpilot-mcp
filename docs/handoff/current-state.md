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
- workbook inventory for sheets, tables, connections, and queries over the COM workbook wrapper
- targeted query refresh with structured success/failure results
- diagnostic query probing via temp-query creation, preview load, and cleanup
- temp-query cleanup with structured partial-failure reporting
- a conservative shared-session safety seam with session diagnostics, attached-session refusal codes, and blocked mutation in attached mode
- a first narrow MCP stdio host surface for inventory, query definition read, targeted refresh, probing, and temp-query cleanup, plus structured host-side argument and invocation errors
- an opt-in live Excel harness with a tracked workbook fixture and real Excel validation for session state, inventory, cleanup, targeted refresh, and probing
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
- eventual safe coexistence between agent operations and active human editing in the same live workbook session

## Immediate gaps

- no broad concurrency/coordination support yet for safe agent work while a human is actively editing the same workbook
- no positive attached-session mutation support yet, even when the session appears otherwise safe
- no richer unsafe-UI detection yet beyond readiness/interactivity/calculation heuristics
- no broad agent-facing workbook edit/range workflow surface yet beyond the narrow internal seams used by the harness
- no first backlog/delegation packet set yet
