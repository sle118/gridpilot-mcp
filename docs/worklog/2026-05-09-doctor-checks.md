# Worklog: 2026-05-09 - doctor checks

## Goal

Implement DEPLOY-006 by adding reusable doctor checks to the deployment core.

## Changes made

- Add `ExcelMcp.Deployment.Doctor` models and runner.
- Check profile load/validation, host executable, runtimeconfig, working directory, log directory writability, stdout policy, and Excel availability.
- Keep Excel probing passive by default, with active COM activation only when explicitly requested.
- Add unit tests with injectable probes so ordinary validation does not require live Excel.
- Update deployment inventory and handoff docs after implementation.

## Constraints

- Do not launch the MCP host or perform MCP protocol smoke testing.
- Do not add tray UI, config writers, or MCP/Excel surface changes.
- Keep live Excel behavior opt-in and keep normal unit tests mock-based.

## Findings

- Doctor checks can reuse the existing profile validator and log locator without referencing the host, proxy, tray, or COM adapter projects.
- Runtimeconfig inspection gives a useful static signal without launching the MCP host.
- Excel availability needs two modes: passive registration checks by default, and active COM activation only when a caller explicitly opts in.

## Decisions taken

- Added injectable probes for Excel availability, runtimeconfig reading, and log directory writability.
- Treated missing runtimeconfig as a warning and malformed runtimeconfig as an error.
- Treated missing log files as non-errors while checking whether their parent directories are writable.
- Kept protocol handshake validation entirely deferred to DEPLOY-007.

## Tests

- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj --no-restore`
  - Passed: 192
  - Failed: 0
  - Skipped: 0
  - Existing CA1416 Windows-platform warnings were emitted from COM/interop-related tests and adapter code.
- Static dependency check found no WinForms, ToolHost, ToolProxy, ComAdapter, Interop, or `Excel.Application` references inside `src/ExcelMcp.Deployment`.

## Next

- Plan DEPLOY-007 MCP smoke test.
