# Next steps

## Immediate priorities

1. Expand shared-session safeguards beyond the current attached-session mutation block.
2. Define unsafe UI-state detection and reporting for attached live sessions.
3. Decide whether any mutating operations can be safely permitted in attached mode under stricter preconditions.
4. Decide whether broader workbook edit and range workflows should be promoted beyond the current narrow internal seams.
5. Package the next bounded work items into small backlog or delegation slices for future agents.

## Suggested first bounded implementation slice

- attached-session safety refinement and unsafe-state reporting
- the next thin MCP host improvements around configuration and error clarity
- mock-first tests plus opt-in live validation for the above

## Cautions

- avoid a broad rename of `ExcelMcp.*` until it is a deliberate tracked task
- do not let branding work trigger large structural churn
- do not design live Excel tests in a way that affects default CI
- keep COM isolated behind interfaces
- do not assume concurrent agent/user workbook mutation is safe until explicit coordination rules and safeguards exist
