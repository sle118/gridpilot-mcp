# Worklog: 2026-05-14 - agent live testing guide

## Goal

Create a repeatable live-testing guide that Codex or other live agents can use to validate GridPilot MCP against real desktop Excel and report results back in a consistent format.

## Scope

- consolidate the current live-test harness rules, environment gates, fixture rules, and reporting expectations
- provide exact commands for standard live tests, attached-session live tests, and focused re-runs
- define a reusable reporting template for pass/fail summaries, environment notes, and blockers
- add navigation pointers so future agents can find the guide quickly

## Notes

- the existing live-test behavior is already reasonably disciplined, but the instructions are spread across `tests/ExcelMcp.LiveTests/README.md`, the testing strategy doc, fixture notes, and older worklogs
- the new guide should keep live testing opt-in, repeatable, and clearly separated from normal CI validation
- the current task is to execute the live-testing guide itself against the local Windows workstation and capture whether the standard and attached-session live suites complete cleanly

## Execution Result

- `dotnet build ExcelMcp.sln -c Release` succeeded
- standard live suite with `RUN_LIVE_EXCEL_TESTS=1` failed: 25 failed, 2 passed, 19 skipped
- attached-session live suite with `RUN_LIVE_EXCEL_TESTS=1` and `RUN_ATTACHED_LIVE_EXCEL_TESTS=1` failed: 44 failed, 2 passed, 0 skipped
- the dominant failure mode was COM disconnection during workbook cleanup (`RPC_E_DISCONNECTED`), with a smaller number of assertion mismatches in workbook edit and calculation tests
- cleaned up the disposable `/automation -Embedding` Excel instances created by the live runs and removed the temp workbook copies created under `.tmp/live-excel/` for this execution
