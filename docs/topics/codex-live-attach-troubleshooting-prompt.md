# Codex Prompt: Live Attach Troubleshooting

Use this prompt when continuing live troubleshooting of GridPilot MCP against a real Excel desktop session.

```text
You are working in the GridPilot MCP repo.

Read first:
- docs/handoff/current-state.md
- docs/handoff/next-steps.md
- docs/architecture/shared-session-safety.md
- docs/worklog/2026-04-29-attached-session-acquisition-refinement.md
- docs/worklog/2026-04-30-runtime-logging-switch.md

Current live-troubleshooting context:
- MCP startup/initialize/tools/list are working.
- session_list_connections works.
- session_list_open_workbooks was initially timing out in ROT discovery, then began succeeding after Excel cleanup.
- Discovery was noisy: it returned one credible workbook plus non-workbook COM/moniker artifacts and temporary PNG entries.
- The only credible workbook discovered was:
  - Book_mcp_test.xlsx
- session_connect_workbook by workbook name failed with:
  - "COM object that has been separated from its underlying RCW cannot be used."
- session_connect_workbook by explicit workbook path failed with the same stale RCW error.
- workbook_list_inventory by workbook path also failed with the same stale RCW error.
- session_list_connections reported no active attached sessions after those failures.
- The host transport later closed after Excel state transitions / cleanup attempts.

Important code changes already made:
- runtime logging switch across host/bridge/COM adapter
- bounded STA timeout around ROT/workbook-owner discovery
- host-side MCP tool-call timeout so tools fail with structured errors instead of hanging
- stricter workbook discovery filtering so only workbook-like Excel entries should survive ROT enumeration
- workbook-owner application discovery was changed so COM application objects are resolved on the caller thread rather than being returned out of the STA discovery worker
- earlier attached-session acquisition work also established that aggressive COM final-release semantics were unsafe for shared attached-session reuse

Relevant files:
- src/ExcelMcp.ToolHost/Mcp/StdioMcpServer.cs
- src/ExcelMcp.ToolHost/Mcp/McpToolServer.cs
- src/ExcelMcp.ToolHost/WorkbookServiceResolver.cs
- src/ExcelMcp.ComAdapter/Interop/RunningWorkbookObjectTable.cs
- src/ExcelMcp.ComAdapter/Interop/ComExcelApplicationHandle.cs
- src/ExcelMcp.ComAdapter/Interop/ComWorkbookHandle.cs
- .tmp/gridpilot-runtime.log

What to do next:
1. Reproduce against the real registered GridPilot host.
2. Validate whether session_list_open_workbooks is now clean and only returns real workbook entries.
3. Validate session_connect_workbook by explicit workbook path first.
4. Verify whether the previously observed stale RCW failure still reproduces after the latest workbook-owner attach change.
5. If attach still fails, use runtime logs plus the 2026-04-29 acquisition worklog to distinguish:
   - a remaining COM apartment / RCW lifetime issue
   - Excel-instance contamination from leaked test sessions
   - a different workbook-owner resolution or borrowed-workbook ownership issue
6. Prefer fixing the real attach/acquisition bug rather than adding retries around stale RCW failures.
7. Keep runtime logging enabled and inspect .tmp/gridpilot-runtime.log after each repro.
8. Add or update focused tests for any behavior change.

Working assumptions:
- MCP transport framing/startup is no longer the main issue; the remaining live failures, if any, are somewhere in workbook discovery, workbook-owner attachment, Excel-instance hygiene, or attached-session COM ownership/lifetime.
- Live Excel state on the workstation can contaminate results, so distinguish product bugs from leaked test Excel instances.
- Do not broaden scope into unrelated workbook-surface work.

Desired outcome:
- Codex can discover a real workbook cleanly.
- session_connect_workbook succeeds for the discovered workbook.
- workbook_list_inventory succeeds through the attached workbook-owner path.
```
