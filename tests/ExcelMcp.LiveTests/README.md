# Live Excel tests

These tests are part of **GridPilot MCP**, but the current starter project still uses the provisional `ExcelMcp.LiveTests` name.

Live tests must remain:

- optional
- workstation-local
- excluded from normal CI
- explicit about workbook fixture setup and cleanup
- careful to restore Excel application state after execution

The tracked baseline fixture now lives at `tests/live/fixtures/test_workbook.xlsx`.

Tests must copy that workbook to a throwaway temp file before opening it in Excel. Never run live tests directly against the tracked fixture.

Environment gates:

- `RUN_LIVE_EXCEL_TESTS=1` enables the standard live suite
- `RUN_ATTACHED_LIVE_EXCEL_TESTS=1` additionally enables the attached-session live tests
