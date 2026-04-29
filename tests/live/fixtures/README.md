# Live Excel fixture

`test_workbook.xlsx` is the tracked baseline workbook fixture for GridPilot MCP live Excel tests.

Current baseline semantics:

- 5 Power Query queries total
- 2 connection-only queries
- 3 queries loaded to worksheets/tables
- one query pair that still contains an error
- one query pair where the error is filtered out or removed
- one direct-load query used specifically for refresh validation after source-table edits

Current named coverage anchors:

- `tbleDirectRefreshLoaded`: direct load from `Table1`, used for live refresh assertions
- `tbleWithErrorRemoved`: stable probe candidate and filtered-error baseline
- `tbleWithErrorOnChangedType`: known-error probe candidate
- `tbleWithErrorRemovedLoaded` and `tbleWithErrorOnChangedTypeLoaded`: loaded inventory coverage

Rules for maintainers:

- Treat the tracked workbook as the immutable baseline fixture
- Live tests must copy it to a temp workbook before opening it in Excel
- Future agents may extend the workbook intentionally, but they must update the live assertions and this metadata note when the baseline contract changes
