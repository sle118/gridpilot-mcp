# Workbook Surface Roadmap

## Purpose

This note captures the current prioritization for broadening GridPilot MCP beyond the initial query-centric and rectangular-range tool surface.

It is intended as a durable planning reference for future agent sessions, not as a rigid delivery plan. Sequence should be revisited if live Excel validation or shared-session safety work changes the risk profile.

## Current implemented surface

The bridge currently supports:

- workbook inventory for sheets, tables, connections, and queries
- workbook save and save-as
- worksheet create, rename, and delete
- workbook and worksheet-scoped name inventory
- named-range reads
- workbook and worksheet-scoped name create/update/delete
- query definition read and query formula edit
- table-aware reads with headers and row payloads
- targeted query refresh
- diagnostic query probing
- temp-query cleanup
- table create, resize, append, replace, delete, and core options updates
- rectangular range read
- multi-range rectangular value write
- compact range formatting read/write, row and column sizing, and autofit
- rectangular formula read/write and clear-contents range operations
- workbook/worksheet/range recalculation
- workbook/worksheet/range error inspection with compact diagnostic hit lists
- worksheet move, copy, reordering, and three-state visibility control
- workbook dependency graph reads
- workbook visibility and workbook protection reads/updates
- query create/rename/delete and richer query detail
- workbook data connection detail, rename, update, and delete

## Next five surfaces to prioritize

1. Better unsafe UI-state detection for attached editing
   - detect active cell edit mode, modal dialogs, and other unsafe interactive states more reliably
   - keep read operations broadly allowed
   - tighten refusal reasons for attached mutating tools

2. Data quality and validation surfaces
   - inspect and manage validation rules
   - inspect conditional formatting presence
   - support overwrite-safety and worksheet hygiene checks

3. Broader workbook structure operations
   - workbook-level protection and visibility controls are now implemented
   - broader workbook layout helpers remain future work

## Following five surfaces after that

4. Query and connection lifecycle
   - query create/delete/rename and richer connection lifecycle are now implemented
   - generic standalone external-connection creation remains intentionally out of scope
   - deeper external-connection authoring remains future work

5. Richer formatting and presentation controls
   - add borders, merged-cell handling, style helpers, or higher-level report-polish workflows on top of the new compact baseline

6. Richer calculation-aware worksheet operations
   - add formula-error inspection summaries, smarter targeting shortcuts, or calculation-state-aware diagnostics on top of the new baseline
   - explore whether calculation/reporting workflows should remain worksheet-centric or expand into broader workbook patching helpers

7. Named-structure and dependency workflows
   - graph-first dependency inspection across names, tables, queries, and connections is now implemented
   - deeper lineage beyond workbook metadata remains future work

8. Structured import and export workflows
   - export ranges, tables, or query outputs to CSV or JSON
   - import tabular payloads into ranges or tables
   - support diagnostics, snapshots, and rollback-friendly workflows

## Why this order

The current recommendation is to stabilize shared-session safety after the new workbook-polish baseline, then continue expanding durable data and workflow surfaces before highly presentation-oriented automation.

That means:

- safety comes before broader mutation
- shared-session safety still comes before broader mutation
- workflow primitives come before richer workbook choreography
- import/export and validation should follow once the bridge can mutate structures more confidently

## Notes

- Query formula edit, range read/write, compact range formatting/autofit, range formula/clear operations, workbook/worksheet/range recalculation, workbook/worksheet/range error inspection, workbook persistence, worksheet lifecycle/layout, and core table mutations are already implemented and should be treated as the baseline edit surface.
- Any newly promoted mutating surface should continue to flow through the shared-session approval and safety seam.
- Live Excel coverage should be extended alongside each new surface, but remain opt-in.
