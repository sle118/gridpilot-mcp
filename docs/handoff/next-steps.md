# Next steps

## Immediate priorities

1. Add the next mutating workbook families on top of the new names baseline: table lifecycle/mutation, worksheet lifecycle, and formula-aware operations.
2. Improve unsafe UI-state detection beyond the current readiness/interactivity-plus-edit/modal heuristics if broader shared-session mutation proves risky.
3. Decide whether the current approval lease should evolve into a stronger coordination model before broader workbook editing is exposed.
4. Package the remaining workbook-surface wave into small backlog or delegation slices for future agents.
5. Extend the live workbook fixture only where the next mutating families need stable validation anchors.

## Suggested first bounded implementation slice

- one next mutating workbook workflow on top of the new names/table-read surface, ideally table lifecycle or worksheet lifecycle
- mock-first tests plus opt-in live validation for the above
- keep broader workbook patching and formatting workflows out of scope in the same slice

## Reference roadmap

- workbook-surface expansion priorities are captured in `docs/topics/workbook-surface-roadmap.md`

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
