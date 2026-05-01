# Shared-session safety

## Intent

GridPilot MCP is meant to support agent work around live Excel sessions, including future coexistence with active human editing. The current bridge therefore treats shared-session safety as a first-class bridge concern rather than an Excel workbook concern.

## Current policy

The MCP host now starts lazily and can track multiple workbook connections at once. Registration/startup is therefore separate from workbook connection and workbook mutation.

Two session acquisition paths are relevant:

- `bridge-owned`: the host opens a workbook in its own bridge-controlled Excel session
- `attached`: the host attaches to the already-running Excel instance that owns a requested workbook

Within attached behavior, the host supports two targeting strategies:

- `workbook-owner`: prefer the running Excel instance that already owns the requested workbook path
- `any-running`: attach to any running Excel instance when workbook-aware targeting is not required

The preferred attached behavior is `workbook-owner`, because it keeps connected live-workbook targeting deterministic.

The current policy is deliberately conservative:

- read-only operations are allowed in either connection mode
- mutating operations in attached mode require an explicit workbook-scoped approval lease
- the bridge does not silently attach to an already-open user workbook and mutate it
- workbook-aware attached acquisition refuses to guess when zero or multiple candidate running instances match the requested workbook path or visible workbook title

Agents are expected to:

1. list open workbooks when needed
2. connect explicitly by workbook title or path
3. carry the returned `connectionId` into later workbook tool calls

Read-only operations currently include:

- workbook inventory
- workbook-name inventory and named-range reads
- query definition reads
- table reads
- range reads

Mutating or diagnostic-write operations currently include:

- name create, update, and delete
- targeted refresh
- query probing
- temp-query cleanup
- query formula edits
- range writes

Future workbook write/edit operations should use the same safety seam.

## Runtime enforcement

The bridge enforces safety before opening the workbook for a mutating action:

1. classify the operation intent
2. inspect session diagnostics such as session mode, readiness, interactivity, calculation state, and current unsafe attached-session heuristics
3. require workbook-owner attachment for attached-session mutation
4. require a valid approval lease for the exact workbook path when attached-session mutation is requested
5. return a structured refusal reason when the action should be blocked

This keeps COM-specific workbook discovery inside the adapter and keeps policy decisions in the bridge.

Current refusal codes are intentionally distinct:

- `attach_target_no_running_instance`
- `attach_target_no_matching_instance`
- `attach_target_multiple_matching_instances`
- `shared_session_approval_required`
- `shared_session_approval_expired`
- `shared_session_approval_scope_mismatch`
- `shared_session_ui_unsafe`
- `shared_session_busy`
- `attached_session_approval_not_applicable`

When the bridge borrows a workbook that is already open inside an attached Excel instance, the workbook handle is treated as borrowed rather than owned:

- read operations may reuse the existing workbook object
- disposing the borrowed handle does not close the user-owned workbook
- mutating operations may proceed only when the attached session is workbook-owner targeted, the session diagnostics are safe, and a valid approval lease exists for that workbook

Approval leases are currently:

- in-memory and host-local
- scoped to one normalized workbook path
- explicitly granted and revoked through MCP tools
- broad across the workbook's attached mutating surface, so one active lease covers later attached mutating tools for that same workbook until expiry or revoke
- observable on the connection surface so clients can see whether an attached workbook currently has an active lease, instead of re-requesting approval blindly
- automatically expired after a configurable TTL, with a default of 10 minutes

Connected-workbook identity is still the normalized workbook path internally, even when an agent connects by visible workbook title and then uses `connectionId` for later calls.

## Save expectations

The bridge owns persistence expectations for mutating operations:

- successful targeted refresh saves the workbook before closing it
- successful temp-query cleanup saves the workbook when deletions occurred
- probe execution cleans temporary artifacts by default and does not widen save behavior beyond what the workbook operation already requires

This avoids hidden differences between tool callers and keeps workbook-close behavior explicit.

## Known limits

- the current unsafe-state detection is still heuristic and relies on readiness, interactivity, calculation state, and derived edit/modal signals rather than deep UI inspection
- it does not yet provide a lease/lock model for coordinated shared mutation
- attached mutation approval is a trust/coordination mechanism, not an authentication system
- multi-workbook connection support does not yet add a stronger shared lock/lease model between human edits and agent edits
- the approved attached mutation surface is still narrow and limited to refresh, probe, temp-query cleanup, query formula edits, name lifecycle, and rectangular range writes
- attached-session live validation is supported, but gated separately because workstation setup still determines whether the intended running workbook owner can be prepared cleanly for attachment

## Next design pressure

The next shared-session step should define:

- explicit open/attach policy per tool or operation class
- how the bridge should detect and report unsafe active-UI states beyond the current readiness/interactivity heuristics
- whether the approval lease should evolve into a stronger coordination/lease model before broader workbook editing is exposed
- whether the current rectangular range write model should stay narrow or grow into a broader workbook patch model
