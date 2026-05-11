# Deployment Core And Tray Governance

This note records the corrected direction for the GridPilot MCP deployment and Windows tray initiative.

The initiative is **deployment core + tray shell**, not tray-first. The tray app should be a human-facing UI over reusable deployment and diagnostics services. It must not own profile loading, agent config generation, doctor checks, smoke testing, log discovery, or diagnostic bundle construction.

## Direction

- Keep the current MCP server, Excel COM/OLE automation, workbook routing, tool semantics, and transport internals unchanged unless a narrow status/reporting interface is explicitly required.
- Add a reusable deployment/diagnostics library first. Use the current repo naming convention for the project name, so prefer `src/ExcelMcp.Deployment` until a dedicated code-level rename is planned.
- Add `src/GridPilot.Tray` later as a WinForms shell over the deployment core.
- Keep the tray optional. Headless/dev use must continue to work without it.
- Keep runtime logs file-backed. MCP stdout must remain JSON-RPC only.

## Layering

Deployment core owns:

- launch profile model, load, and validation
- agent config emitters
- log locator
- diagnostic bundle/report builder
- doctor checks
- MCP smoke-test client

Tray app owns:

- `NotifyIcon` lifecycle
- context menu
- dashboard/forms
- clipboard copy actions
- asynchronous calls into deployment-core services

Do not place deployment-core behavior directly in the tray project. If a tray action needs deployment knowledge, call the shared library.

## Repo-Specific Constraints

- Reuse existing MCP transport lessons before designing smoke testing. Inspect `ExcelMcp.ToolProxy`, `McpFrameSniffer`, and the related integration tests.
- Preserve support for both framed MCP stdio and raw JSON-RPC stdio. Prior Codex work showed clients may differ here.
- The smoke test must detect stdout pollution before or between JSON-RPC messages.
- Treat log path as a first-class deployment concern, not as incidental CLI text.
- Do not add a mutation/policy section to the v1 launch profile unless it maps to an actual enforced host option.
- Add `schemaVersion` to the launch profile from the start.
- Keep diagnostics/logging separate from MCP stdout and proxy transport traces.

## Governance Packets

### DEPLOY-001 - Deployment Inventory and Current Surface Map

Inventory the current deployment surface before designing new abstractions.

Must cover:

- current host executable(s), including `ExcelMcp.ToolHost` and diagnostic proxy surfaces
- current launch args
- supported session modes and attach targets
- environment variables
- log path behavior
- stdout/stderr rules
- existing proxy/framing behavior
- expected `tools/list` shape
- known Codex, VS Code, Claude, Copilot registration examples if present
- build output paths
- packaging assumptions

Acceptance criteria:

- Produces a durable doc under `docs/topics/` or `docs/architecture/`.
- Cites current source files, tests, and setup docs that define the inventory.
- Clearly distinguishes known behavior from proposed behavior.
- Does not change feature code.

Validation:

- Review against `docs/topics/mcp-setup-and-troubleshooting.md`.
- Review against `HostOptions`, `Program`, `ToolNames`, `McpToolServer`, `ExcelMcp.ToolProxy`, and current integration tests.

### DEPLOY-002 - Shared Deployment Core Project

Create the reusable deployment/diagnostics project that later UI and CLI surfaces can consume.

Acceptance criteria:

- Adds `src/ExcelMcp.Deployment` unless a dedicated naming decision says otherwise.
- Contains no WinForms or tray dependencies.
- Provides stable service boundaries for profiles, config emission, logs, doctor checks, diagnostic reports, and smoke tests.
- Is covered by unit tests.

Validation:

- `dotnet test` in a compile-run pass when the MCP host is not locking build outputs.
- Static review confirms no UI dependencies.

### DEPLOY-003 - Canonical Launch Profile Schema

Define the canonical GridPilot MCP launch profile v1.

Profile shape:

```json
{
  "schemaVersion": 1,
  "name": "gridpilot-default",
  "displayName": "GridPilot MCP",
  "host": {
    "command": "C:\\Path\\To\\ExcelMcp.ToolHost.exe",
    "args": [
      "--session-mode",
      "attach",
      "--attach-target",
      "workbook-owner"
    ],
    "workingDirectory": null,
    "env": {
      "GRIDPILOT_LOG_LEVEL": "info"
    }
  },
  "logs": {
    "path": null,
    "stdoutPolicy": "jsonRpcOnly"
  },
  "metadata": {
    "description": "Default local GridPilot MCP launch profile"
  }
}
```

Acceptance criteria:

- Loader reports readable errors for missing files, invalid JSON, unsupported `schemaVersion`, missing command, invalid args/env shapes, and missing executable.
- Validator understands current host args and env vars without launching Excel.
- No v1 mutation policy exists unless the host enforces it.
- Log path and stdout policy are validated as deployment concerns.

Validation:

- Unit tests for valid profile, malformed profile, missing command, bad executable path, bad env shape, and unsupported schema version.

### DEPLOY-004 - Agent Config Emitters

Generate copyable MCP client config snippets from the canonical profile.

Targets:

- VS Code / GitHub Copilot
- Codex CLI
- Claude Code
- generic MCP stdio JSON

Acceptance criteria:

- Generated configs use the canonical profile command, args, working directory, and environment.
- Generation does not require Excel or the MCP host to be running.
- Emitters do not write user config files.
- Target-specific formatting is deterministic and test-covered.

Validation:

- Snapshot or exact-string unit tests for each target.
- Manual comparison against any registration examples found in DEPLOY-001.

### DEPLOY-005 - Log Locator and Diagnostic Bundle

Make deployment/log state easy to inspect and copy.

Acceptance criteria:

- Locates configured log paths and conventional fallback paths.
- Handles missing, locked, empty, and large log files.
- Builds a diagnostic report that includes profile summary, log paths, recent log metadata, host command, stdout policy, and environment summary without leaking unnecessary secrets.
- Does not read huge logs synchronously on a future UI thread.

Validation:

- Unit tests for configured path, null path, missing folder, latest-log selection, locked-file handling, and redaction behavior.

### DEPLOY-006 - Doctor Checks

Add environment and configuration checks separate from protocol smoke testing.

Checks should include:

- profile file exists and is valid
- MCP executable exists and is launchable enough for metadata/config validation
- .NET runtime expectation is satisfied
- Excel appears installed
- Excel COM automation can be reached when appropriate
- log folder exists or can be created
- current user can access required paths
- no obvious stdout pollution mode is configured

Acceptance criteria:

- Doctor never crashes callers.
- Every result has severity, concise message, and suggested next step.
- Doctor does not require Excel to be running for checks that can be performed statically.
- Doctor is callable from non-UI code.

Validation:

- Unit tests for pass/warn/fail result composition.
- Optional live Excel checks remain opt-in.

### DEPLOY-007 - MCP Smoke Test

Verify that the configured host can perform the MCP handshake and list tools.

Acceptance criteria:

- Launches the configured host process with profile command, args, working directory, env, and log path.
- Supports framed and raw JSON-RPC stdio behavior consistent with existing transport tests.
- Sends `initialize`, verifies a valid initialize response, sends `tools/list`, and verifies expected GridPilot tools are present.
- Detects launch failure, invalid JSON, stdout pollution, no-response timeout, missing expected tools, and premature process exit.
- Attempts graceful shutdown/exit and kills the process on timeout.
- Does not leave orphaned MCP processes.

Validation:

- Unit/integration tests with fake child processes for success, timeout, invalid JSON, pollution, missing tools, and shutdown cleanup.
- Review against `ExcelMcp.ToolProxy`, `McpFrameSniffer`, and existing `StdioMcpServer` tests before implementation.

### DEPLOY-008 - Windows Tray Shell

Add the optional WinForms notification-area app as a thin UI over deployment core.

Acceptance criteria:

- App starts without opening a normal window.
- Tray icon appears and disposes cleanly on exit.
- Right-click menu opens.
- Double-click opens dashboard.
- Second instance exits cleanly or activates the first instance.
- Tray code delegates profile, config, doctor, smoke, and log work to deployment core.

Validation:

- Manual Windows verification.
- Unit-test non-UI menu/action composition where practical.

### DEPLOY-009 - Dashboard and Preview UI

Provide a simple user-facing dashboard for deployment-core data.

Acceptance criteria:

- Dashboard shows active profile, command, args, env summary, log path, doctor results, smoke-test results, and recent logs.
- Agent config preview supports copy-to-clipboard.
- Long-running work runs asynchronously and keeps UI responsive.
- UI never writes client config automatically.

Validation:

- Manual Windows verification for responsiveness and copy actions.
- Tests for presenter/view-model logic if introduced.

### DEPLOY-010 - Optional Config Writers

Add conservative config-file writing only after emit/copy behavior is stable.

Acceptance criteria:

- Always previews diff before write.
- Always backs up existing config.
- Never overwrites unknown user content blindly.
- Supports dry-run.
- Reports exact modified file path.

Validation:

- Unit tests for JSON/TOML merge behavior, backup creation, dry-run, cancellation, and write failure.

### DEPLOY-011 - Packaging and Startup

Make the deployment/tray surface easy to install and start.

Acceptance criteria:

- App can run from build output and an installed folder.
- Zip artifact or simple install-dev script exists before MSIX/winget work.
- Startup registration can be enabled and disabled.
- Supported startup flags are documented, including `--startup`, `--no-dashboard`, and `--open-dashboard`.
- Config paths remain stable across updates.

Validation:

- Manual install/uninstall pass on Windows.
- CI artifact inspection where available.

## Execution Order

Use this sequence unless a later handoff explicitly changes priorities:

1. DEPLOY-001 - Deployment Inventory and Current Surface Map
2. DEPLOY-002 - Shared Deployment Core Project
3. DEPLOY-003 - Canonical Launch Profile Schema
4. DEPLOY-004 - Agent Config Emitters
5. DEPLOY-005 - Log Locator and Diagnostic Bundle
6. DEPLOY-006 - Doctor Checks
7. DEPLOY-007 - MCP Smoke Test
8. DEPLOY-008 - Windows Tray Shell
9. DEPLOY-009 - Dashboard and Preview UI
10. DEPLOY-010 - Optional Config Writers
11. DEPLOY-011 - Packaging and Startup

