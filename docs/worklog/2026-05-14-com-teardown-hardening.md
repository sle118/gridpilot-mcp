# Worklog: 2026-05-14 - COM teardown hardening

## Goal
Harden workbook teardown so detached Excel sessions do not turn cleanup into a hard failure during `DisposeAsync` / `CloseAsync`.

## Notes
- The live attached-session pass reproduced `RPC_E_DISCONNECTED` while closing workbook handles.
- The failure path is in the COM teardown/logging boundary, not the workbook operation itself.
- Keep normal close behavior intact, but make cleanup best-effort when Excel has already disconnected.
