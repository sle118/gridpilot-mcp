# Worklog: 2026-04-19 - branding overlay fold-in

## Goal

Fold the GridPilot MCP branding package into the repository structure without causing a disruptive starter-code rename.

## Changes made

- added brand assets under `branding/assets/`
- rewrote root docs to use GridPilot MCP branding
- added a topic document describing naming strategy
- added an ADR covering the branding/code-name split
- updated handoff docs to reflect the branding overlay state

## Findings

The supplied branding package contained visual assets only. It did not include naming policy or repository structure guidance, so those rules were added in docs.

## Decisions taken

GridPilot MCP becomes the repository identity now.

`ExcelMcp.*` remains the provisional code-level namespace and project naming until a later dedicated rename pass.

## Tests

No code execution changes. Documentation and asset overlay only.

## Next

Expand the packs in order and create a clean bootstrap commit before further implementation work.
