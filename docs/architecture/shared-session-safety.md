# Shared-session safety

## Intent

GridPilot MCP is meant to support agent work around live Excel sessions, including future coexistence with active human editing. The current bridge therefore treats shared-session safety as a first-class bridge concern rather than an Excel workbook concern.

## Current policy

Two session modes are relevant:

- `create-new`: the host creates a dedicated hidden Excel instance for bridge work
- `attach`: the host attaches to an already-running Excel instance

Within `attach` mode, the host now supports two targeting strategies:

- `workbook-owner`: prefer the running Excel instance that already owns the requested workbook path
- `any-running`: attach to any running Excel instance when workbook-aware targeting is not required

The default host behavior is `workbook-owner`, because the current MCP tool surface always includes a workbook path.

The current policy is deliberately conservative:

- read-only operations are allowed in either mode
- mutating operations are blocked when the target workbook is already owned by the attached session
- the bridge does not silently attach to an already-open user workbook and mutate it
- workbook-aware attached acquisition refuses to guess when zero or multiple candidate running instances match the requested workbook path

Read-only operations currently include:

- workbook inventory
- query definition reads

Mutating or diagnostic-write operations currently include:

- targeted refresh
- query probing
- temp-query cleanup

Future workbook write/edit operations should use the same safety seam.

## Runtime enforcement

The bridge enforces safety before opening the workbook for a mutating action:

1. classify the operation intent
2. inspect session diagnostics such as session mode, readiness, interactivity, and calculation state
3. inspect `ListOpenWorkbooksAsync(...)` on the current session when attached-session workbook ownership matters
4. return a structured refusal reason when the action should be blocked

This keeps COM-specific workbook discovery inside the adapter and keeps policy decisions in the bridge.

Current refusal codes are intentionally distinct:

- `attach_target_no_running_instance`
- `attach_target_no_matching_instance`
- `attach_target_multiple_matching_instances`
- `shared_session_workbook_owned_in_attached_session`
- `shared_session_ui_unsafe`
- `shared_session_busy`
- `shared_session_attach_mutation_unsupported`

When the bridge borrows a workbook that is already open inside an attached Excel instance, the workbook handle is treated as borrowed rather than owned:

- read operations may reuse the existing workbook object
- disposing the borrowed handle does not close the user-owned workbook
- mutating operations are still blocked by policy unless a future slice explicitly allows them

## Save expectations

The bridge owns persistence expectations for mutating operations:

- successful targeted refresh saves the workbook before closing it
- successful temp-query cleanup saves the workbook when deletions occurred
- probe execution cleans temporary artifacts by default and does not widen save behavior beyond what the workbook operation already requires

This avoids hidden differences between tool callers and keeps workbook-close behavior explicit.

## Known limits

- the current unsafe-state detection is still heuristic and relies on Excel readiness, interactivity, and calculation state rather than richer UI inspection
- it does not yet provide a lease/lock model for coordinated shared mutation
- `attach` mode is still read-heavy today, because mutation remains blocked even when workbook-aware attachment succeeds and the session otherwise appears safe
- attached-session live validation is supported, but gated separately because workstation setup still determines whether the intended running workbook owner can be prepared cleanly for attachment

## Next design pressure

The next shared-session step should define:

- explicit open/attach policy per tool or operation class
- whether any mutating operations can be permitted in attached mode under stricter preconditions
- how the bridge should detect and report unsafe active-UI states beyond the current readiness/interactivity heuristics
- whether a lightweight operation lease model is needed before broader workbook editing is exposed
