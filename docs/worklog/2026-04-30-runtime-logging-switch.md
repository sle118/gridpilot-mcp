# Worklog: 2026-04-30 - Runtime logging switch

## Goal
Add a shared runtime logging facility that can be enabled from the MCP registration command and used across the host, bridge, and COM adapter without contaminating MCP stdout traffic.

## Planned changes
- add shared low-level logging types in the core project
- extend host options with `--log-level` / `--log-path` plus matching environment variables
- thread the logger through the host, bridge, and COM adapter layers
- add focused tests for option parsing, logger behavior, and MCP-safe logging
- document the new registration switches and troubleshooting workflow

## Notes
- runtime logging should default to `off`
- runtime logs should go to a file, not stdout
- proxy wire logging remains a separate troubleshooting tool

## Changes made
- added shared runtime logging types in `ExcelMcp.Core` with `off` / `info` / `debug` / `trace` levels plus file-backed and null implementations
- extended host option parsing with `--log-level`, `--log-path`, `GRIDPILOT_LOG_LEVEL`, and `GRIDPILOT_LOG_PATH`
- threaded the logger through the MCP host, workbook resolver, bridge safety/session services, and COM-backed session/workbook handles
- added focused tests for host option parsing, logger file behavior, and MCP-safe logging during raw-json stdio startup
- documented the new registration switches and the runtime-log-vs-proxy troubleshooting split
- followed up on a live regression where `session_list_open_workbooks` could stall the first `tools/call` by wrapping ROT/workbook-owner discovery in a bounded STA worker that fails fast with a structured Excel-session error
- hardened MCP `tools/call` dispatch with argument-key logging plus a host-side execution timeout so a stalled tool now returns a structured `tool_timeout` error instead of leaving the client waiting indefinitely
- tightened running-workbook discovery so only workbook-like Excel entries survive ROT enumeration, filtering out non-workbook monikers such as unrelated files that were polluting `session_list_open_workbooks`
- adjusted workbook-owner attachment to resolve owner application COM objects on the caller thread instead of returning them out of the STA discovery worker, to avoid stale RCW failures during connect and inventory operations
- added a short Codex handoff prompt document for continuing live attached-session troubleshooting without repeating the same transport and discovery investigation
- refined that Codex prompt so it also points to the 2026-04-29 attached-session acquisition refinement worklog and frames the next live session as a verification/debug pass instead of assuming stale RCW lifetime is still the active blocker

## Tests
- `dotnet build ExcelMcp.sln -nologo`
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo`
