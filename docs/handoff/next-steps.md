# Next steps

## Immediate priorities

1. Implement workbook and query inventory over the new session and workbook seams.
2. Add temp-query cleanup behavior and tests.
3. Add targeted refresh primitives.
4. Decide whether workbook open/attach behavior needs additional policy before broader tool-surface work.
5. Add optional live Excel harness conventions.

## Suggested first bounded implementation slice

- workbook inventory
- query inventory
- query definition read
- cleanup of temporary diagnostic queries
- mock-first tests for the above

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
