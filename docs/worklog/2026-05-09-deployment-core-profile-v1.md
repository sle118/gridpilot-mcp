# Worklog: 2026-05-09 - deployment core profile v1

## Goal

Implement DEPLOY-002/003 by adding a reusable deployment core project and the canonical launch profile v1 model, loader, and validator.

## Changes made

- Added `src/ExcelMcp.Deployment` as a UI-free reusable deployment core class library.
- Added launch profile v1 model, loader, validation result, issue, and validator types under `ExcelMcp.Deployment.Profiles`.
- Added unit tests for valid profile load/validate, missing and invalid profile files, malformed args/env shapes, invalid schema/paths/stdout policy, and absence of mutation policy requirements.
- Added the deployment project to `ExcelMcp.sln` and referenced it from `tests/ExcelMcp.UnitTests`.
- Updated deployment inventory and handoff docs to reflect that the deployment core/profile v1 surface now exists.

## Findings

- The deployment library has no WinForms, ToolHost, ToolProxy, ComAdapter, or COM interop dependency.

## Decisions taken

- Kept code-level naming as `ExcelMcp.Deployment`.
- Kept this slice limited to profile load/validate behavior; emitters, log locator, doctor, smoke test, and tray remain future slices.

## Tests

- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj --no-restore`
  - Passed: 156
  - Failed: 0
  - Skipped: 0
  - Existing CA1416 Windows-platform warnings were emitted from COM/interop-related tests and adapter code.
- Static dependency review: `rg -n "WinForms|System.Windows.Forms|ToolHost|ToolProxy|ComAdapter|Interop|Excel\.Application" src/ExcelMcp.Deployment` returned no matches.

## Next

- Plan DEPLOY-004 agent config emitters.
