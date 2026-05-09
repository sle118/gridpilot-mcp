# GridPilot MCP

![GridPilot MCP hero](branding/assets/github-hero.svg)

GridPilot MCP is a local desktop automation bridge for Microsoft Excel. It gives coding agents a controlled MCP host for inspecting, editing, refreshing, and diagnosing live workbooks without pushing orchestration logic into VBA.

## Why It Matters

- Operate **live desktop Excel** through a dedicated control plane instead of workbook-side scripts.
- Keep edits **deliberate and observable** with connection routing, mutation permissions, and runtime logging.
- Target the real workbook surface: **ranges, queries, tables, names, formatting, worksheet layout, and diagnostics**.
- Preserve a path for safe human + agent coexistence instead of pretending unattended automation is the goal.

## What It Is

GridPilot MCP is the human-facing identity of this repository. The implementation still uses provisional `ExcelMcp.*` project and namespace names inside the solution while the early workbook surface continues to evolve.

The current repo is intentionally split into two layers:

- a governance layer for docs, handoff continuity, testing discipline, and repo workflow
- a working C# bridge that is steadily expanding the live Excel workbook surface

## Why It Exists

The project is built around a few simple rules:

- Excel stays the **data plane**.
- The C# bridge owns **session safety, routing, cleanup, retries, and diagnostics**.
- Agents should get **targeted tools**, not a vague “run whatever in Excel” escape hatch.
- Normal validation should stay **mock-first**, with live Excel remaining opt-in.

![Architecture overview](branding/assets/architecture-overview.svg)

## What You Can Do Today

![Implemented surface map](branding/assets/surface-map.svg)

### Session

**Status:** implemented

- list open workbooks across running Excel instances
- connect by workbook name or full path
- create a new workbook through the bridge
- route later calls through `connectionId`
- list, inspect, and disconnect workbook connections

### Workbook

**Status:** implemented, still expanding

- inventory sheets, queries, connections, and tables
- save in place and save as with connection retargeting
- create, rename, delete, move, copy, and reorder worksheets
- set worksheet visibility including `veryHidden`

### Range

**Status:** implemented and practical

- read and write rectangular values
- read and write formulas
- clear contents while preserving layout
- read and write compact formatting snapshots
- set row height, column width, and autofit
- distinguish true no-fill from explicit fill state

### Query

**Status:** implemented

- read query definitions
- run targeted refresh
- run diagnostic probes
- clean up temp diagnostic queries
- update query formulas

### Table And Names

**Status:** implemented

- read table payloads and metadata
- create, resize, append, replace, delete, and configure tables
- list, resolve, read, create, update, and delete workbook and worksheet-scoped names

### Safety And Diagnostics

**Status:** implemented, next focus is refinement

- workbook-, worksheet-, and range-scoped recalculation
- compact formula and literal error inspection
- mutation-permission leases for attached sessions
- runtime logging across host, bridge, and COM adapter
- structured failures instead of silent UI-driven behavior

## How It Connects

![Normal workbook flow](branding/assets/workflow-overview.svg)

Typical flow:

1. start the MCP host
2. optionally call `session_list_open_workbooks`
3. call `session_connect_workbook`
4. use the returned `connectionId` on later workbook tools

Representative MCP flow:

```text
session_list_open_workbooks
session_connect_workbook { "workbookName": "Budget.xlsx" }
workbook_list_inventory { "connectionId": "..." }
range_read { "connectionId": "...", "sheetName": "Summary", "address": "A1:C10" }
session_disconnect_workbook { "connectionId": "..." }
```

## Launch And MCP Setup

### Recommended host registration

For Codex, prefer registering the built host executable rather than `dotnet run`.

Attached live-session mode:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner
```

Dedicated hidden Excel instance:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode create-new
```

### Runtime logging

Use file-backed runtime logging for real troubleshooting without polluting MCP stdout:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

Supported switches:

- `--log-level off|info|debug|trace`
- `--log-path <file>`

Matching environment variables:

- `GRIDPILOT_LOG_LEVEL`
- `GRIDPILOT_LOG_PATH`

### MCP troubleshooting proxy

When startup or transport behavior needs inspection, register the proxy in front of the host:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolProxy\bin\Debug\net8.0\ExcelMcp.ToolProxy.exe --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\mcp-proxy\gridpilot.log --label gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

### Useful follow-up commands

```powershell
codex mcp list
codex mcp get gridpilot
```

If Codex reports MCP startup timeouts, increase the timeout in `~/.codex/config.toml`:

```toml
[mcp_servers.gridpilot]
startup_timeout_sec = 60
```

## Repo Shape

- `AGENTS.md`
  Fast operational rules for agents working in this repo.
- `branding/assets/`
  Logos, icons, reference boards, and the GitHub presentation kit.
- `docs/handoff/`
  Current state and near-term next actions.
- `docs/topics/`
  Focused technical notes.
- `src/`
  The bridge, host, COM adapter, and supporting projects.
- `tests/`
  Unit, integration, and opt-in live Excel validation.

## Branding Assets

The repo ships with light and dark logos plus reference boards under `branding/assets/`.

- use `logo.svg` on light backgrounds
- use `logo-dark.svg` on dark backgrounds
- keep new GitHub-facing visuals in `branding/assets/` so there is one clear source of truth

## Expected Unzip Order

If reconstructing the workspace from generated packs, unzip in this order:

1. governance pack
2. solution skeleton pack
3. branding overlay pack

## Current Priorities

1. improve attached-session unsafe-UI detection now that formatting and worksheet layout mutations are live
2. decide whether mutation approval should evolve into a stronger coordination model for concurrent human and agent editing
3. package the next workbook-surface slices after the current workbook-polish baseline
4. keep refining runtime logging based on real regression investigations
