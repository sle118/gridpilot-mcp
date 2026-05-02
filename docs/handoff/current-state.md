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
- query formula edit with structured success/failure results and bridge-owned save behavior
- range read and multi-range write with bridge-side preflight validation and bridge-owned save behavior for successful writes
- workbook and worksheet-scoped name inventory, named-range reads, and explicit name create/update/delete operations
- table-aware reads with headers, rows, totals-row metadata, and deeper table metadata reads
- structured table lifecycle and mutation:
  - create table from range
  - resize table
  - append rows
  - replace table body rows
  - delete table
  - set core table options (`hasHeaders`, `showTotals`)
- workbook persistence and worksheet lifecycle:
  - save workbook in place
  - save workbook as a new path with connection retargeting
  - create worksheets
  - rename worksheets
  - delete non-last worksheets
  - connection-targeted workbook operations now serialize per `connectionId`, so `workbook_save_as` retargets the live connection before later same-connection tool calls run
- range formula and clear operations:
  - read formulas with `null` for non-formula cells
  - write formulas into one or more rectangular ranges
  - clear range contents while preserving formatting and layout
- lazy MCP host startup with explicit multi-workbook connection management:
  - list open workbooks across running Excel instances
  - connect by visible workbook title or full path
  - create a brand-new workbook by full path through an explicit bridge-owned create tool
  - reuse connection ids across later workbook tool calls
  - disconnect individual connected workbooks
  - expose attached mutation approval state on connection responses so clients can tell whether one workbook-scoped lease is already active for the current host session
- file-backed runtime logging across the host, bridge, and COM adapter:
  - enabled by `--log-level` / `--log-path` or matching `GRIDPILOT_*` environment variables
  - structured one-line entries for host lifecycle, MCP tool calls, workbook routing, safety checks, and COM session/workbook activity
  - kept separate from the MCP proxy’s raw transport logging
- a mutation-permission seam with session diagnostics, workbook-aware attached-session targeting, richer unsafe-state classification, and workbook-scoped or session-scoped mutation permission leases
- a widened but still structured MCP stdio host surface for session workbook discovery/connect/list/get/disconnect/create, inventory, workbook save/save-as, worksheet create/rename/delete, workbook-name listing, name resolution/read/create/update/delete, query definition read, targeted refresh, probing, temp-query cleanup, query formula edit, table get/read/create/resize/append/replace/delete/options, range read, range write, range formula read/write, range clear, generic mutation permission grant/revoke/status tools, compatibility attached-session grant/revoke shims, plus structured host-side argument and invocation errors
- a level-based runtime logging switch on the MCP host surface intended for real-world regression troubleshooting without polluting MCP stdout
- an opt-in live Excel harness with a tracked workbook fixture and real Excel validation for session state, inventory, cleanup, targeted refresh, probing, and lease-gated attached-session mutation
- a separately gated attached-session live suite that now validates workbook-targeted attachment, read-only inventory and range reads, pre-approval refusal for workbook edits, approved mutation, revoke, and approval expiry against a real running Excel instance
- workbook identity normalization is now aligned across discovery, connect, approval, safety checks, and attached query mutation so URL-style workbook identities can flow end to end without synthetic local-path rewrites
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
- explicit workbook connection after MCP startup rather than eager Excel startup during host initialization

## Immediate gaps

- no broad concurrency/coordination support yet for safe agent work while a human is actively editing the same workbook
- no broad attached-session workbook edit surface yet beyond query edits, workbook save/save-as, worksheet create/rename/delete, name lifecycle, named-range reads, table get/read/create/resize/append/replace/delete/options, range read/write, refresh, probe, and temp-query cleanup
- no broad unsafe-UI detection yet beyond the current readiness/interactivity-plus-edit/modal heuristics
- no broad workbook patching, formatting, worksheet move/copy, recalculation, or formula-error inspection surface yet
- no first backlog/delegation packet set yet
