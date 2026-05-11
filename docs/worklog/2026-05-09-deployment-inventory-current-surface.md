# Worklog: 2026-05-09 - deployment inventory current surface

## Goal

Implement DEPLOY-001 as a documentation-only inventory for the deployment core plus tray shell initiative.

## Changes

- Added a deployment inventory topic covering current host/proxy executables, launch options, environment variables, logging behavior, stdio rules, framed/raw JSON-RPC transport behavior, current tool names, Codex registration examples, client-registration gaps, build paths, and packaging assumptions.
- Linked the inventory topic from the topics index.
- Updated the next-steps handoff to reflect the new deployment-core focus and pause MCP surface expansion.
- Kept the slice descriptive only; no feature code, MCP surface changes, project scaffolding, tray UI, launch profile schema, or mutation policy was added.

## Validation

- Reviewed the inventory against the current setup doc, host/proxy option parsing, stdio server behavior, proxy frame sniffer behavior, tool-name constants, and existing integration tests.
- No tests were run because this slice only changes documentation.
