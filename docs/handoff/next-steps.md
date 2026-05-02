# Next steps

## Immediate priorities

1. Add the next mutating workbook families on top of the new range-formula baseline: recalculation/error inspection, formatting, and broader workbook-structure operations.
2. Promote recalculation and formula/error inspection as the next bounded worksheet family now that formula read/write and clear operations are in place.
3. Improve unsafe UI-state detection beyond the current readiness/interactivity-plus-edit/modal heuristics now that multiple connected workbooks can coexist in one host.
4. Decide whether the current approval lease should evolve into a stronger coordination model before broader workbook editing is exposed.
5. Package the remaining workbook-surface wave into small backlog or delegation slices for future agents.
6. Use the new runtime logging switch during live workbook trials and refine log coverage/field choices based on the first real regression investigations.

## Suggested first bounded implementation slice

- one next mutating workbook workflow on top of the new persistence + worksheet/table + formula-range baseline, ideally recalculation/error inspection
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
- do not let new connected-workbook ergonomics bypass path-scoped mutation approval for attached sessions
- keep runtime logging out of MCP stdout; transport tracing should stay in the separate proxy tool
