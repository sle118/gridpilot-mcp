# 2026-04-30 Workbook Surface Wave Tranche 3

## Summary

Promote table lifecycle and structured table mutation as the next bounded workbook-edit family on top of the existing table-read, range-write, and attached-session approval foundations.

## Planned scope

- add deeper table metadata reads with `table_get`
- add explicit table lifecycle and mutation behavior:
  - create from range
  - resize
  - append rows
  - replace body rows
  - set core options
- keep table mutation behind the existing attached approval and safety seam
- add unit, integration, and opt-in live validation for the new table family

## Completed

- extended the core workbook abstraction with narrow table request/result types for metadata and mutation
- added bridge-owned save-on-success orchestration for:
  - `table_create`
  - `table_resize`
  - `table_append_rows`
  - `table_replace_rows`
  - `table_set_options`
- kept `table_get` and `table_read` approval-free in attached mode while routing all new table mutators through the existing mutation safety gate
- implemented COM-backed table operations in the workbook adapter while keeping raw Excel details isolated there
- widened the MCP host with:
  - `table_get`
  - `table_create`
  - `table_resize`
  - `table_append_rows`
  - `table_replace_rows`
  - `table_set_options`
- added unit, integration, and opt-in live tests for create-new and attached-session table workflows

## Notes

- the tracked live workbook fixture did not need structural changes for this tranche; live tests use stable disposable ranges on copied temp workbooks
- worksheet lifecycle and formula-aware operations remain the next likely mutating families after this table slice
