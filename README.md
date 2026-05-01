# GridPilot MCP

GridPilot MCP is a local desktop automation bridge for Microsoft Excel. It is designed to let coding agents inspect, edit, refresh, and troubleshoot live workbooks through a controlled C# MCP host instead of pushing orchestration logic into VBA.

The current repository bootstrap is intentionally split into two layers:

- a **governance layer** for cross-agent continuity, documentation hygiene, and testing discipline
- a **solution skeleton** for the future bridge, using provisional `ExcelMcp.*` assembly and namespace names inside the starter code

The external project identity is now **GridPilot MCP**. The internal starter code still uses `ExcelMcp.*` as a temporary implementation namespace so the overlay can be applied cleanly after the earlier zip packs without leaving broken references. A dedicated rename pass can be done later once the first working slices are in place.

## Mission

GridPilot MCP will provide a local C# MCP bridge over a live desktop Excel instance. The bridge will own session safety, targeted refresh, Power Query diagnostics, cleanup of temporary artifacts, and a testable abstraction boundary over Excel COM.

## Current structure

- `AGENTS.md`: fast operational entry point for agents
- `CONTRIBUTING.md`: branch, commit, test, and documentation rules
- `branding/assets/`: repository branding assets and source images
- `docs/`: architecture, decisions, topics, handoff, and worklogs
- `src/`: starter implementation projects, currently under provisional `ExcelMcp.*` names
- `tests/`: unit, integration, and optional live Excel tests

## MCP launch and discovery

GridPilot MCP is a console MCP host that is meant to be launched by an MCP client over `stdio`. It is not designed for automatic network discovery or as a long-running HTTP service. In practice, a client such as Codex launches the host process, then negotiates tools over MCP using the process pipes.

The common Codex setup flow is to register the server once with the Codex CLI. The Codex desktop app can then reuse that shared MCP configuration.

Registering the MCP server does not mean Excel starts immediately or that a workbook is already selected. The host now starts lazily and expects agents to explicitly discover and connect workbooks after MCP startup.

For Codex, prefer registering the built host executable rather than `dotnet run`. That avoids extra build-time overhead during MCP startup and is usually more reliable when the client enforces a startup timeout.

Register a dedicated hidden Excel instance:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode create-new
```

Register an attached live-session mode that targets the running Excel instance owning the workbook:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner
```

Enable file-backed runtime logging for real-world troubleshooting:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

Supported runtime logging switches:

- `--log-level off|info|debug|trace`
- `--log-path <file>`

Matching environment variables are also supported:

- `GRIDPILOT_LOG_LEVEL`
- `GRIDPILOT_LOG_PATH`

Useful follow-up commands:

```powershell
codex mcp list
codex mcp get gridpilot
```

If Codex reports that the MCP client timed out during startup, increase the configured startup timeout in `~/.codex/config.toml`:

```toml
[mcp_servers.gridpilot]
startup_timeout_sec = 60
```

Runtime logging is separate from the MCP troubleshooting proxy:

- use host runtime logging first when you want workbook/session/COM lifecycle diagnostics during normal runs
- use the MCP proxy when you need raw client-to-server transport traces

## MCP troubleshooting proxy

When Codex-to-host startup behavior needs deeper inspection, register Codex against the bundled MCP stdio proxy instead of the host directly. The proxy forwards stdin/stdout/stderr unchanged and logs parsed MCP traffic to a file.

Example registration:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolProxy\bin\Debug\net8.0\ExcelMcp.ToolProxy.exe --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\mcp-proxy\gridpilot.log --label gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

The proxy log will capture:

- process launch command and arguments
- client-to-server MCP frames
- server-to-client MCP frames
- wrapped host stderr lines
- wrapped process exit code

## Workbook connection flow

GridPilot MCP now separates MCP registration from workbook use:

1. start the MCP server
2. optionally call `session_list_open_workbooks`
3. call `session_connect_workbook` with either:
   - `workbookName` to attach to an already-open workbook by visible workbook title
   - `workbookPath` to attach if already open, or otherwise open it in a bridge-owned Excel session
4. use the returned `connectionId` on later workbook tool calls

The host can keep multiple workbook connections at once. Existing workbook tools still accept `workbookPath`, but agents can now omit it and pass `connectionId` instead.

Representative tool flow:

```text
session_list_open_workbooks
session_connect_workbook { "workbookName": "Budget.xlsx" }
workbook_list_inventory { "connectionId": "..." }
range_read { "connectionId": "...", "sheetName": "Summary", "address": "A1:C10" }
session_disconnect_workbook { "connectionId": "..." }
```

## Branding assets

The branding package has been folded into the repository under `branding/assets/`.

Included assets:

- `logo.svg`
- `logo-dark.svg`
- `icon.svg`
- `icon-dark.svg`
- two presentation boards as PNG references

## Expected unzip order

If you are reconstructing the workspace from generated packs, unzip in this order:

1. governance pack
2. solution skeleton pack
3. branding overlay pack

The branding overlay is meant to rewrite the human-facing files after the earlier two packs are expanded.

## Near-term priorities

1. lock the first MCP tool contract
2. implement session-state scoping and workbook/query inventory abstractions
3. add mock-first tests for orchestration behavior
4. add optional local-only live Excel validation harness
