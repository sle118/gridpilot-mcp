# Worklog: 2026-04-19 - governance bootstrap

## Goal
Establish a lightweight repository governance and project-memory structure suitable for cross-agent collaboration.

## Changes made
- Added root guidance documents
- Added architecture, decision, topic, handoff, and worklog folders under `docs/`
- Added initial ADRs for architecture direction and documentation governance
- Added initial testing strategy with separate unit, integration, and live Excel tiers

## Findings
A small and explicit documentation surface is preferable to a large narrative-heavy system. Narrative remains useful, but should live in worklogs and topic docs rather than the repo entry surface.

## Decisions taken
- Use `AGENTS.md` as the primary operational entry point for agents
- Keep handoff state in `docs/handoff/`
- Treat live Excel tests as opt-in and local by default

## Tests
Documentation bootstrap only. No code tests were run.

## Next
Define the first MCP tool contract and the corresponding internal Excel abstraction seams for testing.
