# Worklog: 2026-04-29 - live Excel harness

## Goal
Build the first opt-in live Excel harness around the tracked workbook fixture, isolated Excel lifecycle, session state restoration, workbook/query inventory, and temp-query cleanup.

## Changes made
- Moved the tracked baseline workbook fixture to `tests/live/fixtures/test_workbook.xlsx`
- Added live test project scaffolding and helper infrastructure for gating, fixture copy creation, temp cleanup, and isolated Excel lifecycle
- Added live tests for session state restoration, workbook/query inventory, and temp-query cleanup
- Updated the COM adapter to quit owned Excel instances on disposal and implemented query create/update support for live cleanup setup
- Fixed late-bound COM access paths that only surfaced when running against real Excel
- Updated bridge cleanup orchestration to save workbook changes after successful query deletion

## Findings
- The real workbook confirmed one loaded worksheet name is truncated by Excel to fit the sheet-name limit
- Optional COM member access needs DISP_E_MEMBERNOTFOUND handling, not just reflection-style missing-member handling
- Cleanup needed an explicit save at the bridge layer to persist deletions across workbook reopen

## Decisions taken
- Keep the tracked fixture immutable and always run live tests against a copied temp workbook
- Use a dedicated hidden Excel instance created by the test harness rather than attaching to a developer’s existing session
- Limit the first live suite to session state, inventory, and cleanup; defer refresh and probing to later slices

## Tests
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj`
- `dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj`
- `$env:RUN_LIVE_EXCEL_TESTS='1'; dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj`

## Next
- Expand the live harness to cover targeted refresh and query probing once those production behaviors exist.
- Keep concurrent agent-plus-human workbook operation on the roadmap as an explicit design target, not an implicit future assumption.
