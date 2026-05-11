# Deployment Inventory And Current Surface

This is the DEPLOY-001 inventory for the deployment core plus tray shell initiative. It records current repo behavior that later deployment, diagnostics, smoke-test, and tray work must preserve.

This document is descriptive. It does not define new feature behavior, a launch profile schema, a tray UI, or a mutation policy.

## Current Executables

### `ExcelMcp.ToolHost`

`src/ExcelMcp.ToolHost` builds the current MCP server executable.

Expected debug build output:

```text
src/ExcelMcp.ToolHost/bin/Debug/net8.0/ExcelMcp.ToolHost.exe
```

Current responsibilities:

- parse host launch options
- create the runtime logger
- create the workbook service resolver
- run the stdio MCP server against standard input and standard output
- write configuration/startup failures to stderr

Source references:

- `src/ExcelMcp.ToolHost/Program.cs`
- `src/ExcelMcp.ToolHost/HostOptions.cs`
- `src/ExcelMcp.ToolHost/Mcp/StdioMcpServer.cs`

### `ExcelMcp.ToolProxy`

`src/ExcelMcp.ToolProxy` builds the current diagnostic proxy executable.

Expected debug build output:

```text
src/ExcelMcp.ToolProxy/bin/Debug/net8.0/ExcelMcp.ToolProxy.exe
```

Current responsibilities:

- wrap another MCP command after a `--` separator
- pass stdin/stdout/stderr through to the wrapped process
- log transport chunks, parsed MCP frames, parser state, and wrapped process exit status to a proxy log file
- preserve MCP stdout by forwarding the wrapped process stdout to the client

Source references:

- `src/ExcelMcp.ToolProxy/Program.cs`
- `src/ExcelMcp.ToolProxy/ProxyOptions.cs`
- `src/ExcelMcp.ToolProxy/McpFrameSniffer.cs`

### `ExcelMcp.Deployment`

`src/ExcelMcp.Deployment` builds the reusable deployment core library.

Expected debug build output:

```text
src/ExcelMcp.Deployment/bin/Debug/net8.0/ExcelMcp.Deployment.dll
```

Current responsibilities:

- define the canonical launch profile v1 model
- load launch profile JSON from disk
- validate launch profile structure, host command paths, working directory paths, environment shape, and JSON-RPC-only stdout policy
- emit copyable MCP client configuration snippets for supported agent targets
- locate configured and conventional runtime/proxy log paths
- read bounded recent log tails without loading whole large logs into memory
- build markdown-friendly diagnostic reports with environment-value redaction
- run deployment doctor checks for profile, host executable, runtimeconfig, log paths, stdout policy, and Excel availability
- run MCP smoke tests that validate initialize/tools-list protocol behavior

Current non-responsibilities:

- no tray UI
- no references to WinForms, Excel COM adapter internals, `ExcelMcp.ToolHost`, or `ExcelMcp.ToolProxy`

Source references:

- `src/ExcelMcp.Deployment/Profiles/LaunchProfile.cs`
- `src/ExcelMcp.Deployment/Profiles/LaunchProfileLoader.cs`
- `src/ExcelMcp.Deployment/Profiles/LaunchProfileValidator.cs`
- `src/ExcelMcp.Deployment/AgentConfig/AgentConfigEmitter.cs`
- `src/ExcelMcp.Deployment/Logs/DeploymentLogLocator.cs`
- `src/ExcelMcp.Deployment/Logs/RecentLogReader.cs`
- `src/ExcelMcp.Deployment/Diagnostics/DeploymentDiagnosticReportBuilder.cs`
- `src/ExcelMcp.Deployment/Doctor/DoctorRunner.cs`
- `src/ExcelMcp.Deployment/SmokeTests/McpSmokeTestRunner.cs`

### `GridPilot.Tray`

`src/GridPilot.Tray` builds the optional Windows notification-area shell.

Expected debug build output:

```text
src/GridPilot.Tray/bin/Debug/net8.0-windows/GridPilot.Tray.exe
```

Current responsibilities:

- start as a WinForms notification-area app without opening a normal window
- own the `NotifyIcon`, tray context menu, dashboard window, and about dialog
- enforce a named single-instance mutex and exit a second instance cleanly
- resolve profile path from `--profile <path>` and then `GRIDPILOT_PROFILE_PATH`
- show missing/invalid/loaded profile status
- call deployment-core services for MCP config copy, doctor checks, smoke tests, log folder discovery, and diagnostic report copy
- show a tabbed dashboard for overview, agent config preview/copy, doctor results, smoke-test results, and recent log tails

Current non-responsibilities:

- no MCP/Excel internals
- no profile loading, config generation, doctor, smoke, log, or diagnostic logic beyond calls into `ExcelMcp.Deployment`
- no config-file writing
- no startup registration
- no installer, MSIX, or winget packaging
- no automatic setup wizard or finished installer behavior

Source references:

- `src/GridPilot.Tray/Program.cs`
- `src/GridPilot.Tray/TrayApplicationContext.cs`
- `src/GridPilot.Tray/TrayProfileContext.cs`
- `src/GridPilot.Tray/DashboardForm.cs`

## Host Launch Surface

The host supports these command-line switches:

| Switch | Values | Default |
| --- | --- | --- |
| `--session-mode` | `attach`, `create-new`, `new` | `create-new` |
| `--attach-target` | `workbook-owner`, `workbook`, `any-running`, `any` | `workbook-owner` |
| `--visible` | flag only | false |
| `--log-level` | `off`, `info`, `debug`, `trace` | `off` |
| `--log-path` | file path | null unless logging is enabled |

Environment variables are read before command-line arguments, and command-line arguments override environment-derived values:

| Environment variable | Purpose | Default |
| --- | --- | --- |
| `GRIDPILOT_SESSION_MODE` | session mode | `create-new` |
| `GRIDPILOT_ATTACH_TARGET` | attach target | `workbook-owner` |
| `GRIDPILOT_SESSION_VISIBLE` | visible Excel instance when set to `1` | false |
| `GRIDPILOT_LOG_LEVEL` | runtime log level | `off` |
| `GRIDPILOT_LOG_PATH` | runtime log file path | null |

When the effective log level is not `off` and no log path is supplied, the host defaults to:

```text
<current working directory>/.tmp/gridpilot-runtime.log
```

Current host parse failures return exit code `2` and write a configuration error to stderr. Other startup failures return exit code `3` and write a startup error to stderr.

## Proxy Launch Surface

The proxy supports these command-line switches before the required `--` separator:

| Switch | Values | Default |
| --- | --- | --- |
| `--log-path` | file path | `<current working directory>/.tmp/mcp-proxy/<label>.log` |
| `--label` | text label for log categories | `mcp-proxy` |

Everything after `--` is treated as the wrapped MCP command and its arguments.

Current proxy parse failures return exit code `2` and write a configuration error to stderr. If the wrapped process cannot be started, the proxy returns exit code `3`. Otherwise it returns the wrapped process exit code.

## Runtime Logging And Stdio Rules

Runtime logs are file-backed. Diagnostics and runtime logging must not be written to MCP stdout.

Current stdout/stderr behavior:

- `ExcelMcp.ToolHost` uses stdout for MCP JSON-RPC responses only.
- `ExcelMcp.ToolHost` writes configuration and startup errors to stderr.
- `ExcelMcp.ToolProxy` forwards wrapped stdout to its own stdout.
- `ExcelMcp.ToolProxy` forwards wrapped stderr to stderr and also records it in the proxy log.
- Proxy diagnostics are written to the proxy log file, not to stdout.

Future deployment-core and tray work must preserve the JSON-RPC-only stdout rule. Smoke testing must treat unexpected stdout text before or between JSON-RPC messages as stdout pollution.

## MCP Stdio Transport Behavior

The host currently supports two stdio message styles:

- framed MCP messages with `Content-Length` headers
- headerless raw JSON-RPC objects or arrays

For framed messages, the host accepts both `\r\n\r\n` and `\n\n` header terminators. For raw JSON-RPC, the host detects a leading `{` or `[` after ignorable whitespace and reads a balanced JSON value.

The response mode mirrors the detected request mode:

- framed request -> framed response with `Content-Length`
- raw JSON-RPC request -> raw JSON response followed by a newline

The proxy sniffer mirrors this diagnostic knowledge. It can parse framed messages, framed messages across chunks, and raw JSON across chunks.

Regression-sensitive tests:

- `tests/ExcelMcp.IntegrationTests/StdioMcpServerTests.cs`
- `tests/ExcelMcp.IntegrationTests/McpFrameSnifferTests.cs`

Future smoke-test work must inspect and preserve this behavior rather than assuming a single stdio framing style.

## Current Tools/List Surface

The canonical current tool-name inventory is defined in `src/ExcelMcp.Bridge/Contracts/ToolNames.cs` and emitted by `McpToolServer.ListTools()`.

Current tool names:

```text
session_list_open_workbooks
session_connect_workbook
session_create_workbook
session_list_connections
session_get_connection
session_disconnect_workbook
workbook_save
workbook_save_as
workbook_list_inventory
workbook_list_names
worksheet_create
worksheet_rename
worksheet_delete
worksheet_move
worksheet_copy
worksheet_set_visibility
query_get
name_get
name_read
name_create
name_update
name_delete
query_refresh
query_run_probe
query_cleanup_temp
query_set_formula
table_get
table_read
table_create
table_resize
table_append_rows
table_replace_rows
table_delete
table_set_options
range_read
range_write
range_get_format
range_set_format
range_autofit
range_get_formulas
range_set_formulas
range_clear
calculation_recalculate
calculation_inspect_errors
session_grant_mutation_permission
session_revoke_mutation_permission
session_get_mutation_permission
attached_session_grant_mutation
attached_session_revoke_mutation
```

Smoke tests should not require every tool schema to be deeply validated in DEPLOY-007, but they should verify that `tools/list` returns a valid tool list and that expected GridPilot tool names are present.

## Known Client Registration Examples

Current durable setup guidance exists for Codex in `docs/topics/mcp-setup-and-troubleshooting.md`.

Attached live-session mode:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner
```

Dedicated hidden Excel instance:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode create-new
```

Runtime logging:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

Proxy diagnostics:

```powershell
codex mcp add gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolProxy\bin\Debug\net8.0\ExcelMcp.ToolProxy.exe --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\mcp-proxy\gridpilot.log --label gridpilot -- C:\Users\sle11\Documents\VSCode\gridpilot-mcp\src\ExcelMcp.ToolHost\bin\Debug\net8.0\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner --log-level info --log-path C:\Users\sle11\Documents\VSCode\gridpilot-mcp\.tmp\gridpilot-runtime.log
```

The same setup doc also records that Codex startup timeouts may need:

```toml
[mcp_servers.gridpilot]
startup_timeout_sec = 60
```

## Known Client Registration Gaps

The deployment core can emit copyable snippets for:

- VS Code / GitHub Copilot
- Codex CLI
- Claude Code
- generic MCP stdio JSON

The emitted snippets are preview/copy surfaces only. They do not write user config files and should not be treated as a finished setup wizard or installer.

The repo does not yet contain durable per-client setup guides beyond the existing Codex setup and troubleshooting topic.

## Agent Config Emitter Surface

`ExcelMcp.Deployment.AgentConfig` currently supports:

| Target | Suggested file | Format | Notes |
| --- | --- | --- | --- |
| VS Code / GitHub Copilot | `mcp.json` | JSON | Emits `servers.<name>` with `type`, `command`, `args`, and `env`. Working directory is not emitted and produces a warning when present. |
| Codex CLI | `config.toml` | TOML | Emits `[mcp_servers.<name>]` with `command`, `args`, optional `cwd`, `enabled`, and env table. |
| Claude Code | `.mcp.json` | JSON | Emits `mcpServers.<name>` with `type`, `command`, `args`, and `env`. Working directory is not emitted and produces a warning when present. |
| Generic MCP JSON | `mcp.json` | JSON | Emits `mcpServers.<name>` with `type`, `command`, `args`, optional `cwd`, and `env`. |

Emitter behavior:

- validates the launch profile before emitting
- returns structured errors and no content for invalid profiles
- returns structured warnings for target-specific omissions
- uses `profile.Name` as the server id without normalization
- uses LF line endings for deterministic snippet output

## Log Locator And Diagnostic Report Surface

`ExcelMcp.Deployment.Logs` currently supports reusable log discovery and bounded recent-log reading.

Log discovery candidates are returned in deterministic order:

1. `profile.logs.path` when configured
2. `GRIDPILOT_LOG_PATH` from `profile.host.env` when configured
3. conventional runtime log at `<workingDirectory or current directory>/.tmp/gridpilot-runtime.log`
4. conventional proxy log at `<workingDirectory or current directory>/.tmp/mcp-proxy/<profile.Name>.log`

Log metadata reports:

- log kind
- path
- existence
- size in bytes when readable
- last-write timestamp when readable
- access status of missing, accessible, or unreadable
- readable message for missing or unreadable files

`RecentLogReader` reads from the end of the file with line and byte bounds. It handles missing, empty, large, and locked/unreadable log files without throwing through the public result API, and normalizes returned line endings to LF-shaped line data.

`ExcelMcp.Deployment.Diagnostics` currently builds a markdown-friendly deployment diagnostic report containing:

- profile name, display name, and description
- host command, args, working directory, and stdout policy
- environment summary with key-name-based redaction for sensitive keys such as token, secret, key, password, and credential
- discovered log candidates and metadata
- optional bounded recent log tails when explicitly requested

The diagnostic report builder does not launch the MCP host, run doctor checks, run smoke tests, or write to client config files.

## Doctor Check Surface

`ExcelMcp.Deployment.Doctor` currently supports reusable, UI-free deployment health checks.

Doctor checks report a check id, display name, severity, message, and suggested next step. Severities are:

- `Ok`
- `Warning`
- `Error`

The doctor currently checks:

- profile file existence, load, and validation
- host executable existence
- adjacent runtimeconfig existence, readability, framework name, and framework major-version compatibility with the current runtime
- configured working directory existence
- discovered log candidates and log directory writability
- `jsonRpcOnly` stdout policy
- Excel availability

Excel probing is passive by default. The default probe checks that the current OS is Windows and that Excel desktop COM registration is present. Active Excel COM activation is available only when callers set `AllowActiveExcelComProbe`; that mode attempts activation and cleanup, but remains opt-in so normal doctor runs do not start Excel.

The doctor does not send MCP protocol messages, launch a handshake smoke test, write MCP client config files, or require tray UI.

## MCP Smoke Test Surface

`ExcelMcp.Deployment.SmokeTests` currently supports reusable MCP protocol smoke testing.

The smoke test currently:

- loads and validates a launch profile
- launches the configured host command directly with profile args, working directory, and environment
- supports framed MCP requests and raw JSON-RPC requests
- reads framed responses using `Content-Length` with `\r\n\r\n` or `\n\n` header terminators
- reads raw balanced JSON responses
- detects stdout pollution before a valid framed or raw JSON-RPC response
- sends `initialize`
- sends `tools/list`
- verifies a deployment-owned default subset of expected GridPilot tool names
- captures a bounded stderr tail
- attempts graceful shutdown and kills the child process if cleanup times out or fails

The default expected tool subset is:

```text
session_list_open_workbooks
session_connect_workbook
workbook_list_inventory
range_read
range_write
calculation_inspect_errors
```

The smoke-test client does not call workbook tools, does not require a live workbook, does not route through `ExcelMcp.ToolProxy`, and does not change host transport behavior.

Normal unit coverage uses fake child processes. Optional real-host integration coverage is gated by:

```text
RUN_GRIDPILOT_REAL_MCP_SMOKE_TESTS=1
```

## Build And Packaging Assumptions

The current projects inherit `net8.0` from `Directory.Build.props`.

Current packaging state:

- optional tray app exists as a build-output WinForms shell
- deployment core exists as a reusable class library, not as an executable
- no installer exists
- no MSIX or winget package exists
- normal setup uses build output paths and MCP client registration commands

Future packaging work should treat build-output/dev registration as the current baseline, not as a finished install experience.

## Future Work Boundaries

This inventory records current behavior only. Later slices may add:

- optional config writers
- packaging and startup registration

This inventory does not add any v1 mutation policy and does not imply a tray-first architecture. The next implementation slices should continue to keep MCP/Excel internals unchanged unless a narrow deployment diagnostic interface is explicitly planned.
