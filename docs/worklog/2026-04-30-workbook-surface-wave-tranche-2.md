# 2026-04-30 Workbook Surface Wave Tranche 2

## Summary

Continue the workbook-surface epic by hardening attached-session diagnostics and promoting the workbook-name surface from read-only inventory/read into a full lifecycle with explicit MCP tools.

## Planned scope

- extend session diagnostics with clearer unsafe attached-session signals
- tighten shared-session refusal classification around those diagnostics
- add name create, update, and delete behavior for workbook and worksheet-scoped names
- wire the new name lifecycle through the bridge and MCP host
- add unit, integration, and opt-in live validation for the new safety and name lifecycle behavior

## Completed

- extended `SessionDiagnostics` with edit-mode, modal-UI, and busy flags derived from the Excel application handle
- tightened shared-session mutation blocking so edit-like and modal-like states return clearer `shared_session_ui_unsafe` refusals
- promoted workbook names from read-only support to full lifecycle:
  - `name_create`
  - `name_update`
  - `name_delete`
- added bridge-owned save behavior for successful name mutations
- added unit and integration coverage for name lifecycle and the refined unsafe-state gating
- added live Excel validation for create-new name lifecycle and attached-session approval-gated name creation

## Notes

- this tranche intentionally keeps the remaining epic bounded to one coherent vertical slice
- later tranches can build table mutation, worksheet lifecycle, and formula-aware operations on top of the same safety and host patterns
