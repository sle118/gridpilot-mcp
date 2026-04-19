# GridPilot MCP

GridPilot MCP is a local desktop automation bridge for Microsoft Excel. It is designed to let coding agents inspect, edit, refresh, and troubleshoot live workbooks through a controlled C# MCP host instead of pushing orchestration logic into VBA.

The current repository bootstrap is intentionally split into two layers:

- a **governance layer** for cross-agent continuity, documentation hygiene, and testing discipline
- a **solution skeleton** for the future bridge, using provisional `ExcelMcp.*` assembly and namespace names inside the starter code

The external project identity is now **GridPilot MCP**. The internal starter code still uses `ExcelMcp.*` as a temporary implementation namespace so the overlay can be applied cleanly after the earlier zip packs without leaving broken references. A dedicated rename pass can be done later once the first working slices are in place.

## Mission

GridPilot MCP will provide a local C# MCP bridge over a live desktop Excel instance. The bridge will own session safety, targeted refresh, Power Query diagnostics, cleanup of temporary artifacts, and a testable abstraction boundary over Excel COM.

## Current structure

- `AGENTS.md`: fast operational entry point for agents
- `CONTRIBUTING.md`: branch, commit, test, and documentation rules
- `branding/assets/`: repository branding assets and source images
- `docs/`: architecture, decisions, topics, handoff, and worklogs
- `src/`: starter implementation projects, currently under provisional `ExcelMcp.*` names
- `tests/`: unit, integration, and optional live Excel tests

## Branding assets

The branding package has been folded into the repository under `branding/assets/`.

Included assets:

- `logo.svg`
- `logo-dark.svg`
- `icon.svg`
- `icon-dark.svg`
- two presentation boards as PNG references

## Expected unzip order

If you are reconstructing the workspace from generated packs, unzip in this order:

1. governance pack
2. solution skeleton pack
3. branding overlay pack

The branding overlay is meant to rewrite the human-facing files after the earlier two packs are expanded.

## Near-term priorities

1. lock the first MCP tool contract
2. implement session-state scoping and workbook/query inventory abstractions
3. add mock-first tests for orchestration behavior
4. add optional local-only live Excel validation harness
