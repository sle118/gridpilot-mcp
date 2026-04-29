# Live Excel fixture

`test_workbook.xlsx` is the tracked baseline workbook fixture for GridPilot MCP live Excel tests.

Current baseline semantics:

- 4 Power Query queries total
- 2 connection-only queries
- 2 queries loaded to worksheets/tables
- one query pair that still contains an error
- one query pair where the error is filtered out or removed

Rules for maintainers:

- Treat the tracked workbook as the immutable baseline fixture
- Live tests must copy it to a temp workbook before opening it in Excel
- Future agents may extend the workbook intentionally, but they must update the live assertions and this metadata note when the baseline contract changes
