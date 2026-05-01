# Worklog: 2026-05-01 - persistence and worksheet lifecycle slice

## Goal
Implement the next bounded workbook mutation tranche on top of the stabilized connection and mutation-permission model:

- `workbook_save`
- `workbook_save_as`
- `worksheet_create`
- `worksheet_rename`
- `worksheet_delete`
- `table_delete`

## Planned changes
- extend the workbook abstraction with narrow save-as, worksheet lifecycle, and table delete methods
- add structured bridge results and save-on-success orchestration for the new operations
- extend the MCP host surface and workbook resolver so `workbook_save_as` can retarget an existing connection in place
- add mock-first unit and integration coverage for success, safety blocking, and resolver connection retargeting

## Notes before implementation
- the current bridge already saves on success for existing mutating operations, so the new surface should follow that pattern
- `workbook_save_as` needs special handling because it changes workbook identity, connection tracking, and workbook-scoped mutation permission projection
- worksheet lifecycle is the repo’s next prioritized bounded mutation family in the roadmap and handoff docs

## Implementation notes
- extended the workbook abstraction, COM workbook wrapper, bridge service, resolver, and MCP host with `workbook_save`, `workbook_save_as`, `worksheet_create`, `worksheet_rename`, `worksheet_delete`, and `table_delete`
- `workbook_save_as` now validates destination rules, keeps the same connection id, and retargets the connection to the new workbook identity on success
- bridge-owned results project mutation permission as `not_applicable`, while attached-session save/save-as continue to flow through the existing workbook-aware safety seam
- added mock-first unit and integration coverage for save/save-as, worksheet lifecycle, table delete, and connection retargeting
- added opt-in live coverage for bridge-owned persistence plus worksheet/table lifecycle and for attached approval-gated worksheet/table deletion

## Validation
- `dotnet build ExcelMcp.sln -nologo -nodeReuse:false -p:UseSharedCompilation=false`
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo`
- live tests were extended but not run as part of ordinary validation because they remain opt-in workstation checks
