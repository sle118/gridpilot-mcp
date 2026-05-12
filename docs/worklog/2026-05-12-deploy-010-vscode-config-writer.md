# Worklog: 2026-05-12 - DEPLOY-010 VS Code config writer

## Goal

Implement a conservative deployment-core writer for the VS Code / GitHub Copilot user `mcp.json` file.

## Scope

- add a reusable deployment-core service
- target the Windows user-level VS Code `mcp.json` path by default
- merge only `servers.gridpilot`
- preserve unrelated JSON content
- add timestamped backups before real writes
- support dry-run preview with diff/summary
- keep tray/setup integration out of this slice

## Notes

- share installed host command/args/env defaults with profile bootstrap so previewed and written config do not drift
- keep failure behavior conservative for malformed JSON or incompatible root shapes
- expose the writer in the tray as an explicit preview-and-confirm action, but keep installer/setup auto-write out of scope
