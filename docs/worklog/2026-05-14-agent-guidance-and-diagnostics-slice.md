# Worklog: 2026-05-14 - agent guidance and diagnostics slice

## Goal
Implement a guidance-first MCP diagnosis surface so agents can keep workbook targeting state, inspect runtime/session health, tail relevant logs, build redacted diagnostic reports, and control host logging verbosity during live validation.

## Notes
- Keep deployment-core log discovery and bounded tail logic reusable.
- Keep host/runtime-specific diagnosis MCP-exposed rather than tray-owned.
- Preserve explicit workbook targeting; do not introduce hidden current-workbook state.
