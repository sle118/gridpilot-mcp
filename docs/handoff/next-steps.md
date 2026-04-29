# Next steps

## Immediate priorities

1. Improve unsafe UI-state detection beyond the current readiness/interactivity/calculation heuristics now that attached mutation is lease-gated.
2. Decide whether the current approval lease should evolve into a stronger coordination model before broader workbook editing is exposed.
3. Choose the next single narrow workbook-edit surface to promote behind the same attached-session approval gate.
4. Keep range read/write and query-formula editing internal until that next promoted surface is explicitly chosen.
5. Package the next bounded work items into small backlog or delegation slices for future agents.

## Suggested first bounded implementation slice

- unsafe-state detection refinement plus one next narrow attached-session workbook-edit capability
- mock-first tests plus opt-in live validation for the above
- keep the remaining workbook edit/query edit tools internal in the same slice

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
