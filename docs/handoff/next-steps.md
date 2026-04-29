# Next steps

## Immediate priorities

1. Improve unsafe UI-state detection beyond the current readiness/interactivity/calculation heuristics now that attached mutation is lease-gated.
2. Decide whether the current approval lease should evolve into a stronger coordination model before broader workbook editing is exposed.
3. Decide whether the current rectangular range-write and query-edit surface should stay narrow or grow into a broader workbook patch workflow.
4. Choose the next single higher-level workflow to promote behind the same attached-session approval gate, such as query authoring helpers or workbook patch operations.
5. Package the next bounded work items into small backlog or delegation slices for future agents.

## Suggested first bounded implementation slice

- unsafe-state detection refinement plus one next higher-level workbook workflow on top of the new edit surface
- mock-first tests plus opt-in live validation for the above
- keep broader workbook patching and formatting workflows out of scope in the same slice

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
