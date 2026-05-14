# Agent Live Testing Guide

## Purpose

Use this guide when a live agent needs to validate GridPilot MCP against a real Windows desktop Excel environment and report back in a way another agent can reproduce.

This guide is for:

- opt-in live Excel test execution
- focused manual validation against installed releases or local builds
- repeatable result reporting
- MCP-first diagnosis capture during live failures

This guide is not for:

- default CI validation
- Linux-only runners
- ad hoc testing against user workbooks

For the separate workflow where an agent installs or launches GridPilot, registers the MCP host, and drives the product end to end, use:

- `docs/topics/agent-driven-gridpilot-validation-guide.md`

## Read First

Before running live tests, read:

- `docs/handoff/current-state.md`
- `docs/handoff/next-steps.md`
- `docs/architecture/testing-strategy.md`
- `tests/ExcelMcp.LiveTests/README.md`
- `tests/live/fixtures/README.md`

If the work is attached-session specific, also read:

- `docs/architecture/shared-session-safety.md`
- `docs/topics/codex-live-attach-troubleshooting-prompt.md`
- `docs/topics/mcp-setup-and-troubleshooting.md`

## Environment Contract

Live agents should assume and verify the following:

- Windows workstation
- desktop Excel installed
- repo checkout available locally
- live tests remain opt-in and must not be treated as normal CI
- the tracked workbook fixture must stay immutable

Do not:

- run against a real user workbook
- modify `tests/live/fixtures/test_workbook.xlsx` in place
- leave stray Excel instances open after the run
- treat a skipped live run as a product pass

## Environment Variables

Standard live-suite gate:

- `RUN_LIVE_EXCEL_TESTS=1`

Attached-session live-suite gate:

- `RUN_ATTACHED_LIVE_EXCEL_TESTS=1`

Optional workbook override:

- `EXCEL_LIVE_TEST_WORKBOOK=<full path to a disposable workbook fixture>`

PowerShell example:

```powershell
$env:RUN_LIVE_EXCEL_TESTS = '1'
$env:RUN_ATTACHED_LIVE_EXCEL_TESTS = '1'
# Optional:
# $env:EXCEL_LIVE_TEST_WORKBOOK = 'C:\path\to\alternate-fixture.xlsx'
```

## Fixture Rules

The default baseline fixture is:

- `tests/live/fixtures/test_workbook.xlsx`

Rules:

- always test against a copied temp workbook
- never edit the tracked fixture directly
- if you intentionally change fixture semantics, update `tests/live/fixtures/README.md`

The existing harness already creates copied temp workbooks under:

- `.tmp/live-excel/`

## Preflight Checklist

Before running anything, confirm:

1. You are on Windows.
2. Excel is installed.
3. No important user workbooks are open in Excel.
4. The repo builds locally, or you understand why build outputs may be locked.
5. You know whether you are testing:
   - standard live suite
   - attached-session suite
   - installed release / tray / setup manual flow

If the MCP host is already running and has locked outputs, stop it first or use a separate compile/run path before live validation.

## Recommended Run Order

Use this order unless the task calls for something narrower:

1. `dotnet build ExcelMcp.sln -c Release`
2. standard live suite
3. attached-session live suite
4. if a failure occurs, use the MCP diagnosis surface before cleanup
5. focused rerun for failures
6. manual release or tray validation, if the task requires installed-surface checks

## MCP Diagnosis Workflow For Live Failures

When the failure involves the real MCP host, attached workbook-owner mode, or an opaque COM/session issue such as `RPC_E_DISCONNECTED`, capture diagnosis before closing Excel or disconnecting the session.

Recommended order:

1. note the exact failing tool call
2. call `session_get_diagnostics` with the same `connectionId` or workbook target
3. call `diagnostics_get_runtime`
4. if logging is too light, call `diagnostics_set_log_level` with `trace` and rerun the failing step once
5. call `diagnostics_read_log_tail` for the runtime log
6. call `diagnostics_build_report` with recent tails included
7. only then clean up or retry broader environment changes

Useful diagnosis calls:

```json
{ "level": "trace", "scope": "runtime" }
```

```json
{ "kind": "runtime", "maxLines": 120 }
```

```json
{ "connectionId": "<your connection id>", "includeRecentLogTails": true }
```

After the run, restore the level with:

```json
{ "level": "default", "scope": "both" }
```

## Standard Live Suite

Run:

```powershell
$env:RUN_LIVE_EXCEL_TESTS = '1'
dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -c Release --no-restore
```

This covers the standard live harness, including real Excel-backed checks such as:

- workbook and query inventory
- query formula updates
- range reads and writes
- formatting and autofit flows
- refresh and probe workflows
- temp-query cleanup
- recalculation and error inspection

If you only want a focused class:

```powershell
$env:RUN_LIVE_EXCEL_TESTS = '1'
dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -c Release --no-restore --filter "LiveWorkbookEditTests"
```

## Attached-Session Live Suite

Run:

```powershell
$env:RUN_LIVE_EXCEL_TESTS = '1'
$env:RUN_ATTACHED_LIVE_EXCEL_TESTS = '1'
dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -c Release --no-restore
```

Use this when validating:

- workbook-owner attachment
- attached-session mutation approval
- shared-session safety refusals
- attached mutating workflows after approval

If attached-session tests skip, capture the exact skip reason. A skip is often environmental rather than product-fatal, but it is still actionable information.

## Manual Installed-Release Validation

Use this when the task involves setup, tray, or GitHub release behavior rather than only the repo test harness.

Minimum recommended flow:

1. Download or build the current release payload.
2. Unzip to a disposable folder.
3. If the slice involves setup, run `GridPilot.Setup.exe`.
4. If the slice involves tray behavior, run `GridPilot.Tray.exe`.
5. Validate the targeted scenario only.
6. Record:
   - exact release version
   - install scope
   - any environment-specific caveats

Examples:

- setup per-user install
- machine-wide install with elevation
- startup enable/disable
- VS Code config preview/write action
- Copilot manifest compatibility
- tray doctor or smoke-test behavior

## Focused Workbook Lifecycle Validation

For the newer workbook lifecycle slice, the live checklist should cover:

1. Query create
2. Query rename
3. Query delete
4. Query-owned connection behavior
5. Connection rename
6. Connection update
7. Connection delete where Excel permits it
8. Dependency graph read
9. Workbook visibility read/set
10. Workbook protection read/set

For each item, report:

- workbook used
- exact tool or service path exercised
- expected behavior
- actual behavior
- cleanup outcome

## Failure Handling

When something fails:

1. Capture the exact command.
2. Capture the first failing test name or exact manual step.
3. Capture the full exception or structured error message.
4. Note whether the problem is:
   - environment
   - test harness
   - product behavior
   - unclear / needs deeper triage
5. Do not silently rerun multiple times without recording the first failure.

If the failure is attached-session related, also note:

- whether Excel already had open instances
- whether attachment succeeded before the failing operation
- whether the failure was a skip, timeout, stale COM object issue, or structured approval refusal
- the exact output from `session_get_diagnostics`
- the effective log level and path from `diagnostics_get_runtime`
- whether a diagnostic report was captured before cleanup

## Cleanup Expectations

After the run:

- close any Excel instances opened by the test run
- confirm temp workbooks under `.tmp/live-excel/` are not left behind unexpectedly
- if you raised runtime logging for diagnosis, restore it to `default`
- clear temporary environment variables if needed

PowerShell cleanup example:

```powershell
Remove-Item Env:RUN_LIVE_EXCEL_TESTS -ErrorAction SilentlyContinue
Remove-Item Env:RUN_ATTACHED_LIVE_EXCEL_TESTS -ErrorAction SilentlyContinue
Remove-Item Env:EXCEL_LIVE_TEST_WORKBOOK -ErrorAction SilentlyContinue
```

## Reporting Template

Use this format when reporting back:

```md
# Live Test Report

## Scope
- standard live suite / attached-session suite / installed release validation
- target slice or bug

## Environment
- machine:
- Windows version:
- Excel version:
- repo commit or release tag:
- workbook source:

## Commands Run
- `...`
- `...`

## Results
- Passed:
- Failed:
- Skipped:

## Key Findings
- ...
- ...

## Diagnosis Artifacts
- `session_get_diagnostics`:
- `diagnostics_get_runtime`:
- `diagnostics_read_log_tail` summary:
- `diagnostics_build_report` captured: yes/no

## Failures
1. Test or step:
   Error:
   First observed behavior:
   Likely category: environment / harness / product / unknown

## Cleanup
- temp workbook cleanup:
- Excel process cleanup:

## Recommended Next Step
1. ...
```

## Short Agent Prompt

When handing this work to another live agent, use a prompt like:

```text
Run the GridPilot MCP live validation flow using docs/topics/agent-live-testing-guide.md.

Scope:
- <insert target slice or bug>

Required output:
- completed live test report using the repo template
- exact commands run
- exact failures or skip reasons
- clear separation between environment issues and product issues
```
