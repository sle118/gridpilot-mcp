# Worklog: 2026-05-02 - README capabilities and formula range slice

## Goal
Implement a user-facing README capabilities matrix and add the next range-oriented workbook tools:

- `range_set_formulas`
- `range_get_formulas`
- `range_clear`

## Planned changes
- add a README capability-status table that distinguishes implemented, partial, and future capability families
- add an `AGENTS.md` maintenance rule so the README table stays in sync with implemented and newly recognized surfaces
- extend the workbook abstraction, bridge service, COM workbook wrapper, resolver, and MCP host with formula-read, formula-write, and clear-contents range tools
- add mock-first unit and integration coverage plus opt-in live coverage for formula writes, formula reads, and content clearing

## Notes before implementation
- the existing range value read/write surface provides the request parsing, shape-validation, and mutation-safety pattern to follow
- `range_clear` is intentionally scoped to clear contents only, preserving formatting and layout
- the README capability table should stay family-level and readable rather than trying to mirror the full Excel object model member-by-member

## Implementation notes
- added a new README capabilities matrix with implemented, partial, and future capability families, including explicit future coverage for VBA project manipulation
- added an `AGENTS.md` rule to keep the README capabilities matrix current when surfaces are implemented, expanded, or newly recognized
- extended the workbook abstraction, bridge service, COM workbook wrapper, MCP tool server, and test doubles with:
  - `range_set_formulas`
  - `range_get_formulas`
  - `range_clear`
- formula writes reuse the existing range preflight pattern, including rectangular-shape validation against the target range
- formula reads return `null` for non-formula cells instead of exposing constants through the formula surface
- range clear uses `ClearContents`, preserving formatting and layout
- extended opt-in live coverage for both bridge-owned and attached-session formula/clear flows

## Validation
- `dotnet build ExcelMcp.sln -nologo -nodeReuse:false -p:UseSharedCompilation=false`
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo`
- `dotnet test tests/ExcelMcp.LiveTests/ExcelMcp.LiveTests.csproj -nologo`
- live Excel execution remained opt-in and was skipped under the normal environment gates
