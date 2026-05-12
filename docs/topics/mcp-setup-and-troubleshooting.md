# MCP Setup And Troubleshooting

This is the operational reference for registering GridPilot MCP with a client, enabling runtime logs, and troubleshooting startup behavior.

## Public release ZIP

If you downloaded a GitHub release archive, unpack it first. The ZIP contains the host, proxy, tray shell, README, and setup docs in a release folder rooted at `gridpilot-mcp-vX.Y.Z-windows-x64`.

Useful entry points from the unpacked archive:

- `GridPilot.Tray.exe`
- `host\ExcelMcp.ToolHost.exe`
- `proxy\ExcelMcp.ToolProxy.exe`

The same client-registration and troubleshooting guidance below applies after you point your MCP client at the extracted `host` or `proxy` executable. The tray shell is the top-level dashboard entry point.

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
session_disconnect_workbook { "connectionId": "..." }
```
