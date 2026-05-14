# MCP Setup And Troubleshooting

This is the operational reference for registering GridPilot MCP with a client, enabling runtime logs, and troubleshooting startup and workbook-session behavior.

## Public release ZIP

If you downloaded a GitHub release archive, unpack it first. The ZIP contains the host, proxy, tray shell, README, and setup docs in a release folder rooted at `gridpilot-mcp-vX.Y.Z-windows-x64`.

Useful entry points from the unpacked archive:

- `GridPilot.Setup.exe`
- `GridPilot.Tray.exe`
- `host\ExcelMcp.ToolHost.exe`
- `proxy\ExcelMcp.ToolProxy.exe`

Recommended usage:

- run `GridPilot.Setup.exe` if you want a stable installed layout under `%LocalAppData%\GridPilot MCP\app` or `%ProgramFiles%\GridPilot MCP`
- use `GridPilot.Tray.exe` directly when you want the fully portable unzip-and-run path
- point your MCP client at either the extracted or installed `host\ExcelMcp.ToolHost.exe`

The tray shell remains the top-level dashboard entry point.

## Setup wizard

`GridPilot.Setup.exe` supports:

- per-user install without administrator rights
- machine-wide install with elevation
- optional Windows startup registration for the tray app
- update, repair, and uninstall flows

Startup registration launches:

```text
GridPilot.Tray.exe --startup --no-dashboard
```

The tray also supports:

- `--startup`
- `--no-dashboard`
- `--open-dashboard`

## Recommended host registration

For Codex, prefer registering the built host executable rather than `dotnet run`.

### Attached live-session mode

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner
```

### Dedicated hidden Excel instance

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode create-new
```

## Build from source

For a local clone, build the solution and then point your client at the output you want to use:

```powershell
dotnet build ExcelMcp.sln -c Release
```

The exact executable path depends on the project you choose and the configuration you built.

## Runtime logging

Use file-backed runtime logging when you need workbook/session/COM diagnostics without polluting MCP stdout.

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

Supported switches:

- `--log-level off|info|debug|trace`
- `--log-path <file>`

Matching environment variables:

- `GRIDPILOT_LOG_LEVEL`
- `GRIDPILOT_LOG_PATH`

## MCP-first diagnosis tools

Once the host is running, prefer the MCP diagnosis tools before restarting the process or guessing at attach failures.

New diagnosis tools:

- `session_get_diagnostics`
- `diagnostics_get_runtime`
- `diagnostics_list_logs`
- `diagnostics_read_log_tail`
- `diagnostics_build_report`
- `diagnostics_set_log_level`

Typical use:

1. connect and retain the returned `connectionId`
2. if a workbook-targeted call fails or the host behaves unexpectedly, call `session_get_diagnostics`
3. call `diagnostics_get_runtime` to confirm effective log level, log path, schema profile, and tracked connections
4. call `diagnostics_set_log_level` with `debug` or `trace` when you need richer logs
5. call `diagnostics_read_log_tail` or `diagnostics_build_report` before disconnecting or cleaning up

The host also returns guidance-first result metadata on many workbook-targeted calls:

- `guidance.targetContext`
- `guidance.recommendedNextTools`
- `guidance.workflowHints`

When target resolution or attached-session operations fail, structured remediation hints may also be present with:

- `hintCode`
- `message`
- `recommendedTool`
- `suggestedArguments`

Use those hints to reuse the same `connectionId`, re-check workbook targeting, or inspect session state before retrying.

### Raise logging during a live run

Example tool call:

```json
{
  "level": "trace",
  "scope": "both"
}
```

Notes:

- `runtime` changes the current host process only
- `persistent` updates GridPilot's per-user diagnostics override for future launches
- `both` does both
- `default` clears the persistent override and resets the current process to the non-override baseline

Persistent diagnostics settings are stored by GridPilot under `%LocalAppData%\\GridPilot MCP\\diagnostics\\runtime-settings.json`.

### Inspect the effective runtime state

`diagnostics_get_runtime` returns the current effective log level and effective log path. Use it after changing log level so you know exactly which file to inspect.

### Tail the runtime log

Example tool call:

```json
{
  "kind": "runtime",
  "maxLines": 120
}
```

You can also pass an explicit `path` if you want to tail a specific discovered log file.

### Build a redacted diagnostic report

Use `diagnostics_build_report` when you need a copy/paste artifact for investigation:

```json
{
  "connectionId": "<your connection id>",
  "includeRecentLogTails": true
}
```

This captures runtime facts, optional session diagnostics, discovered log metadata, and bounded recent tails without writing anything to MCP stdout beyond the structured tool result.

## MCP troubleshooting proxy

When startup or transport behavior needs inspection, register the proxy in front of the host:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolProxy\bin\Debug\net8.0\ExcelMcp.ToolProxy.exe --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\mcp-proxy\gridpilot.log --label gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

## Helpful follow-up commands

```powershell
codex mcp list
codex mcp get gridpilot
```

If Codex reports MCP startup timeouts, increase the timeout in `~/.codex/config.toml`:

```toml
[mcp_servers.gridpilot]
startup_timeout_sec = 60
```

## Workbook connection flow

Typical tool flow after MCP startup:

```text
session_list_open_workbooks
session_connect_workbook { "workbookName": "Budget.xlsx" }
workbook_list_inventory { "connectionId": "..." }
range_read { "connectionId": "...", "sheetName": "Summary", "address": "A1:C10" }
session_get_diagnostics { "connectionId": "..." }
diagnostics_get_runtime {}
diagnostics_read_log_tail { "kind": "runtime" }
session_disconnect_workbook { "connectionId": "..." }
```

If the client loses track of the workbook after planning or data-gathering, look for the returned `guidance.targetContext.connectionId` and reuse it on later calls instead of reconnecting blindly.
