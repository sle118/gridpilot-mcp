# Worklog: 2026-05-09 - format fill reset regression

## Goal
Fix the formatting regression where:

- `range_set_format` could not restore cells to true Excel no-fill state
- `range_get_format` conflated explicit white fill with no-fill

## Context
- live tester cleanup on `Sheet1!D20:E21` showed that using `fillColor = "#FFFFFF"` as a cleanup stand-in left an explicit solid white fill behind
- direct COM inspection confirmed the bridge was leaving `Pattern = 1` / `ColorIndex = 2` instead of true no-fill semantics

## Planned changes
- extend the formatting contract with explicit fill presence semantics
- make format readback distinguish no-fill from explicit filled cells
- make format writes able to clear fill state back to Excel no-fill
- add unit, integration, and opt-in live regression coverage

## Implemented
- added `HasFill` to the compact range-format snapshot and patch contract
- updated the COM format reader to treat Excel `Pattern = xlPatternNone` as true no-fill and to suppress `fillColor` in that state
- updated the COM format writer so `HasFill = false` restores true no-fill via `Interior.Pattern = xlPatternNone` and `ColorIndex = xlColorIndexNone`
- kept `fillColor` for explicit fills, which now implies a solid fill state instead of being used as a cleanup surrogate
- added mock-first unit/integration coverage plus an opt-in live regression that verifies fill can be restored back to no-fill

## Validation
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -p:OutDir=%TEMP%\\gridpilot-codex-test-out-2\\ --no-restore`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -p:OutDir=%TEMP%\\gridpilot-codex-test-out-2\\ --no-restore`
- `dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -p:OutDir=%TEMP%\\gridpilot-codex-test-out-2\\ --no-restore`
- results:
  - unit tests passed: `138/138`
  - integration tests passed: `79/79`
  - live test assembly compiled successfully; live cases remained skipped under the existing opt-in gates
