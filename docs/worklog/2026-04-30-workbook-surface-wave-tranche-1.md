# 2026-04-30 Workbook Surface Wave Tranche 1

## Summary

Start the first implementation tranche of the broader workbook-surface wave by extending the read-oriented workbook surface with named-range inventory/read support and table-aware reads.

## Planned scope

- add workbook name inventory and resolution
- add named-range reads
- add table-aware read output beyond rectangular range-only access
- wire the new read surfaces through the bridge and MCP host
- add unit, integration, and opt-in live coverage for the new read paths

## Notes

- this tranche intentionally stays on the read-heavy side of the larger wave so it can land cleanly on the existing shared-session safety model
- later slices can build mutating worksheet, table, formatting, and import/export behavior on top of the same bridge and host patterns
