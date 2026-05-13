# Worklog: 2026-05-13 - Copilot manifest compatibility

## Goal

Stop GitHub Copilot / VS Code MCP manifest crashes caused by brittle client-side handling of array-heavy tool schemas.

## Scope

- detect VS Code / GitHub Copilot clients during initialize
- expose a more conservative tool-input schema profile for array-heavy tools
- preserve the richer default schema profile for other MCP clients
- accept string-encoded JSON payloads for the conservative profile without changing the underlying workbook operations
- add tests so the conservative profile is validated automatically

## Notes

- current evidence points to a client-side schema compatibility problem, not only malformed server schemas
- the conservative profile should avoid array-typed input parameters for the write-heavy tools that currently crash or disable in Copilot
- the server should continue to accept the original structured arguments for other MCP clients
