# Next steps

## Immediate priorities

1. Stabilize the broadened workbook-polish baseline now that recalculation/error inspection, formatting, and worksheet layout operations are all implemented.
2. Improve unsafe UI-state detection beyond the current readiness/interactivity-plus-edit/modal heuristics now that formatting and worksheet layout mutations are live.
3. Decide whether the current approval lease should evolve into a stronger coordination model before even broader workbook editing is exposed.
4. Package the next workbook-surface wave into small backlog or delegation slices for future agents, especially validation/conditional-formatting, richer workbook layout, and dependency-aware workflows.
5. Use the runtime logging switch during live workbook trials and refine log coverage/field choices based on the first real regression investigations.

## Suggested first bounded implementation slice

- improve unsafe attached-session UI detection and refusal reasons for the broadened workbook-polish surface
- pick the next post-polish family as a focused slice, likely validation/conditional-formatting diagnostics or broader workbook layout/protection helpers
- keep broader shared-session coordination redesign out of the same slice

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
