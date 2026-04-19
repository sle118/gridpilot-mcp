# Next steps

## Immediate priorities

1. Expand the generated packs into a fresh workspace and create a clean bootstrap commit.
2. Confirm repo naming, solution naming, and whether the temporary `ExcelMcp.*` internal names stay in place for the first iteration.
3. Lock the first implementation slice in an ADR and handoff update.
4. Implement the session abstraction and scoped application-state restore layer.
5. Implement workbook and query inventory.
6. Add temp-query cleanup behavior and tests.
7. Add targeted refresh primitives.
8. Add optional live Excel harness conventions.

## Suggested first bounded implementation slice

- attach/open/close workbook session surface
- session option scoping
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
