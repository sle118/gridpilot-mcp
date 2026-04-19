# Live Excel tests

These tests are part of **GridPilot MCP**, but the current starter project still uses the provisional `ExcelMcp.LiveTests` name.

Live tests must remain:

- optional
- workstation-local
- excluded from normal CI
- explicit about workbook fixture setup and cleanup
- careful to restore Excel application state after execution
