# Worklog: 2026-05-09 - MCP smoke test

## Goal

Implement DEPLOY-007 by adding a reusable MCP smoke-test client to the deployment core.

## Changes made

- Add `ExcelMcp.Deployment.SmokeTests` models, process abstraction, stdio helpers, and runner.
- Support framed MCP and raw JSON-RPC request modes.
- Detect stdout pollution, invalid JSON, launch failure, timeout, premature exit, missing tools, stderr output, and cleanup failures.
- Add fake-process unit tests for normal validation and opt-in real-host integration coverage.
- Update deployment inventory and handoff docs after implementation.

## Constraints

- Do not change MCP/Excel internals.
- Do not add tray UI or config writers.
- Do not require live Excel or a real host for ordinary unit tests.
- Keep deployment core free of ToolHost, ToolProxy, Bridge, ComAdapter, Interop, and WinForms references.

## Findings

- The current host mirrors framed vs raw JSON-RPC response mode based on the request mode, so the smoke-test client supports both request styles.
- The current host does not implement MCP `shutdown`; the smoke-test cleanup path treats shutdown failure as a warning and kills the process to avoid orphaning it.
- Default expected tool names can stay deployment-owned as a stable subset without adding a deployment-core reference to bridge contracts.
- Normal integration output can be locked by running `ExcelMcp.ToolHost` processes, so separate build output is useful for validation when needed.

## Decisions taken

- Made framed MCP the default smoke-test request mode.
- Added raw JSON request mode through `McpSmokeTestOptions`.
- Kept optional real-host smoke coverage gated by `RUN_GRIDPILOT_REAL_MCP_SMOKE_TESTS=1`.
- Launched the configured host directly rather than routing through `ExcelMcp.ToolProxy`.

## Tests

- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj --no-restore`
  - Passed: 206
  - Failed: 0
  - Skipped: 0
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj --no-restore`
  - Failed during build because existing `ExcelMcp.ToolHost` processes were locking default build output DLLs.
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj --no-restore -p:BaseOutputPath="$PWD\.tmp\integration-smoke-build\"`
  - Passed: 79
  - Failed: 0
  - Skipped: 1
  - The skipped test was the opt-in real-host MCP smoke test.
- Static dependency check found no WinForms, ToolHost, ToolProxy, ComAdapter, Interop, or `Excel.Application` references inside `src/ExcelMcp.Deployment`.

## Next

- Plan DEPLOY-008 Windows tray shell.
