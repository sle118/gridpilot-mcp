# Next steps

## Immediate priorities

1. Add targeted refresh primitives over the new workbook and query seams.
2. Implement query probing behavior without widening the MCP surface prematurely.
3. Decide whether workbook open/attach behavior needs additional policy before broader tool-surface work.
4. Add optional live Excel harness conventions.

## Suggested first bounded implementation slice

- targeted refresh
- query probing
- mock-first tests for the above

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
