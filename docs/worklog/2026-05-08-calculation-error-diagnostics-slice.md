# Worklog: 2026-05-08 - calculation and error diagnostics slice

## Goal
Implement the next calculation-aware worksheet surface:

- `calculation_recalculate`
- `calculation_inspect_errors`

## Planned changes
- extend the workbook abstraction, bridge service, COM workbook wrapper, MCP tool host, and test doubles with targeted recalculation and error inspection
- support `workbook`, `worksheet`, and `range` target scopes for both tools
- keep recalculation behind the existing mutation-safety seam without adding implicit workbook saves
- return compact diagnostic hit lists for formula cells and literal error cells
- add mock-first unit and integration coverage plus opt-in live coverage for recalculation and inspection flows
- refresh README capabilities and handoff/roadmap docs so the next surface ordering stays current

## Notes before implementation
- the existing range formula slice is the closest behavioral template for workbook targeting, structured results, and safety enforcement
- error inspection should stay read-only, even when it exposes formula text and current Excel error states
- v1 should favor a compact hit list over a full rectangular payload so diagnostics stay readable across larger worksheets

## Validation
- `dotnet build ExcelMcp.sln -nologo -nodeReuse:false -p:UseSharedCompilation=false`
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -nologo`

## Implementation notes
- added calculation/error request and result models plus workbook-handle abstraction methods for targeted recalculation and error inspection
- extended the bridge service with:
  - mutating safety-gated recalculation
  - read-only error inspection
  - scoped target validation for `workbook`, `worksheet`, and `range`
  - structured logging for recalculation and inspection outcomes
- extended the MCP host surface with:
  - `calculation_recalculate`
  - `calculation_inspect_errors`
  - target-shape argument validation for worksheet/range selectors
- extended the COM workbook wrapper with workbook/worksheet/range `Calculate` calls and used-range/range scanning for formula and literal error hits
- added unit, integration, and opt-in live coverage for the new surface
- refreshed README and handoff/roadmap docs so recalculation/error diagnostics move from planned to implemented

## Validation notes
- the default solution build hit the expected locked-output problem because a local `ExcelMcp.ToolHost` process was running
- validation completed through alternate temp output paths instead:
  - `dotnet build src/ExcelMcp.ToolHost/ExcelMcp.ToolHost.csproj -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:OutputPath=$env:TEMP\\gridpilot-out\\toolhost\\`
  - `dotnet build tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:OutputPath=$env:TEMP\\gridpilot-out\\unittests\\`
  - `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo --no-build -p:OutputPath=$env:TEMP\\gridpilot-out\\unittests\\`
  - `dotnet build tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:OutputPath=$env:TEMP\\gridpilot-out\\integrationtests\\`
  - `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo --no-build -p:OutputPath=$env:TEMP\\gridpilot-out\\integrationtests\\`
  - `dotnet build tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:OutputPath=$env:TEMP\\gridpilot-out\\livetests\\`
  - `dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -nologo --no-build -p:OutputPath=$env:TEMP\\gridpilot-out\\livetests\\`
- live Excel execution remained opt-in and was skipped under the environment gates

## Follow-up adjustments after attached-bridge validation
- late-bound workbook-scope recalculation through `Workbook.Calculate` failed in the live attached bridge with `DISP_E_UNKNOWNNAME`, so workbook scope now recalculates by iterating workbook worksheets and calling worksheet-level `Calculate`
- the literal-error live fixture target needed the real worksheet name `tbleWithErrorOnChangedTypeLoade` rather than the longer query-style name
- the recalc live checks now use `Sheet1` scratch cells instead of loaded-query sheet cells so value reads are less likely to be distorted by inherited table formatting
- a second live bridge pass showed that vertical single-column MCP formula writes could be stored as literal text even though single-cell writes worked, so multi-cell formula writes now marshal through a 1-based COM variant matrix
- added regression coverage for the vertical single-column MCP formula shape and relaxed the worksheet-scope live inspection expectation to match the current fixture contents rather than assuming a built-in literal error on that sheet
