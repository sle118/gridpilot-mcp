# Workbook Surface Roadmap

## Purpose

This note captures the current prioritization for broadening GridPilot MCP beyond the initial query-centric and rectangular-range tool surface.

It is intended as a durable planning reference for future agent sessions, not as a rigid delivery plan. Sequence should be revisited if live Excel validation or shared-session safety work changes the risk profile.

## Current implemented surface

The bridge currently supports:

- workbook inventory for sheets, tables, connections, and queries
- workbook and worksheet-scoped name inventory
- named-range reads
- query definition read and query formula edit
- table-aware reads with headers and row payloads
- targeted query refresh
- diagnostic query probing
- temp-query cleanup
- rectangular range read
- multi-range rectangular value write

## Next five surfaces to prioritize

1. Better unsafe UI-state detection for attached editing
   - detect active cell edit mode, modal dialogs, and other unsafe interactive states more reliably
   - keep read operations broadly allowed
   - tighten refusal reasons for attached mutating tools

2. Named ranges and table-aware reads
   - enumerate workbook and worksheet names
   - resolve named ranges to addresses and values
   - add table-aware read paths so agents can work against stable structures instead of raw coordinates

3. Table operations
   - create tables from ranges
   - inspect table schema more deeply
   - resize tables
   - write rows into tables
   - toggle core attributes such as headers and totals row

4. Worksheet lifecycle
   - create worksheets
   - rename worksheets
   - delete disposable worksheets
   - support temp-sheet workflows cleanly

5. Formatting and presentation controls
   - inspect and change basic formatting
   - support common report-polish scenarios
   - keep formatting behind the same safety expectations as other mutating operations

## Following five surfaces after that

6. Query and connection lifecycle
   - create, delete, and rename queries
   - create or update connections
   - inspect dependency relationships between queries, connections, tables, and load targets

7. Formula and calculation-aware worksheet operations
   - distinguish formula writes from plain value writes
   - trigger targeted recalculation
   - inspect formula and cell error states directly

8. Workbook structure operations
   - move or copy worksheets
   - manage visibility and workbook working layout more intentionally
   - support broader workbook orchestration while keeping COM isolated

9. Data quality and validation surfaces
   - inspect and manage validation rules
   - inspect conditional formatting presence
   - support overwrite-safety and worksheet hygiene checks

10. Structured import and export workflows
   - export ranges, tables, or query outputs to CSV or JSON
   - import tabular payloads into ranges or tables
   - support diagnostics, snapshots, and rollback-friendly workflows

## Why this order

The current recommendation is to prioritize durable data and workflow surfaces before cosmetic or highly presentation-oriented automation.

That means:

- safety comes before broader mutation
- named structures and tables come before styling
- workflow primitives come before richer workbook choreography
- import/export and validation should follow once the bridge can mutate structures more confidently

## Notes

- Query formula edit and range read/write are already implemented and should be treated as the baseline edit surface.
- Any newly promoted mutating surface should continue to flow through the shared-session approval and safety seam.
- Live Excel coverage should be extended alongside each new surface, but remain opt-in.
