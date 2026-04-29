# Shared-session safety

## Intent

GridPilot MCP is meant to support agent work around live Excel sessions, including future coexistence with active human editing. The current bridge therefore treats shared-session safety as a first-class bridge concern rather than an Excel workbook concern.

## Current policy

Two session modes are relevant:

- `create-new`: the host creates a dedicated hidden Excel instance for bridge work
- `attach`: the host attaches to an already-running Excel instance

The current policy is deliberately conservative:

- read-only operations are allowed in either mode
- mutating operations are blocked when the target workbook is already open in the attached session
- the bridge does not silently attach to an already-open user workbook and mutate it

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
2. inspect `ListOpenWorkbooksAsync(...)` on the current session
3. compare the target workbook path against already-open workbooks in that session
4. return a structured `shared_session_unsafe` error when the action should be blocked

This keeps COM-specific workbook discovery inside the adapter and keeps policy decisions in the bridge.

## Save expectations

The bridge owns persistence expectations for mutating operations:

- successful targeted refresh saves the workbook before closing it
- successful temp-query cleanup saves the workbook when deletions occurred
- probe execution cleans temporary artifacts by default and does not widen save behavior beyond what the workbook operation already requires

This avoids hidden differences between tool callers and keeps workbook-close behavior explicit.

## Known limits

- the current guard only reasons about workbooks already open in the current Excel session
- it does not yet detect all unsafe UI states such as an in-progress human cell edit or modal dialog
- it does not yet provide a lease/lock model for coordinated shared mutation
- `attach` mode is therefore best treated as read-heavy today, with mutation blocked whenever the workbook is already live in that attached session

## Next design pressure

The next shared-session step should define:

- explicit open/attach policy per tool or operation class
- whether any mutating operations can be permitted in attached mode under stricter preconditions
- how the bridge should detect and report unsafe active-UI states
- whether a lightweight operation lease model is needed before broader workbook editing is exposed
