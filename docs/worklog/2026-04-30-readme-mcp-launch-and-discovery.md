# Worklog: 2026-04-30 - README MCP launch and discovery

## Goal
Document how GridPilot MCP is typically launched by an MCP client and how Codex discovers a local stdio server configuration.

## Planned changes
- add a short README section describing the local stdio host model
- document the common Codex CLI registration flow that the Codex desktop app can reuse
- include representative `create-new` and `attach` launch examples

## Changes made
- added a README section explaining that the host is a console MCP server intended to be spawned over `stdio`
- documented Codex registration examples using `codex mcp add`
- noted that Codex desktop picks up shared MCP configuration from the Codex CLI and IDE tooling

## Findings
- the repository README described project intent but did not yet explain practical MCP client registration and discovery
- the existing host design fits the normal MCP client pattern well because it already reads from standard input and writes to standard output

## Decisions taken
- keep the new README guidance lightweight and user-facing rather than duplicating deeper architecture details

## Tests
- not run; documentation-only change

## Next
- consider adding a future repo-local config example once the preferred team distribution pattern for Codex MCP entries is decided
