# Next steps

## Immediate priorities

1. Decide whether any mutating operations can be safely permitted in attached mode now that workbook-owner targeting is deterministic.
2. Improve unsafe UI-state detection beyond the current readiness/interactivity/calculation heuristics.
3. Decide whether attached-mode save/ownership rules need a lightweight lease or explicit operator-confirmed policy before any mutation is allowed.
4. Keep range read/write and query-formula editing internal until attached-session safety is stronger, then choose one narrow workbook-edit surface to promote.
5. Package the next bounded work items into small backlog or delegation slices for future agents.

## Suggested first bounded implementation slice

- attached-session mutation policy refinement on top of workbook-owner targeting plus stricter unsafe-state reporting
- mock-first tests plus opt-in live validation for the above
- keep workbook edit/query edit tools internal in the same slice

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
