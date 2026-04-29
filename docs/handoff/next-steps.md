# Next steps

## Immediate priorities

1. Define workbook open/attach and coordination policy for safe agent work alongside active human editing.
2. Add explicit concurrency safeguards for mutating operations before shared-session workflows are considered supported.
3. Expose the first narrow MCP tool surface over the now-live-validated inventory, refresh, probe, and cleanup operations.
4. Decide whether broader workbook edit and range workflows should be promoted beyond the current narrow internal seams.
5. Package the next bounded work items into small backlog or delegation slices for future agents.

## Suggested first bounded implementation slice

- shared-session policy and preconditions for mutating operations
- a thin MCP host surface over existing workbook behaviors
- mock-first tests plus opt-in live validation for the above

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
