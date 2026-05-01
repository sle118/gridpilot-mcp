# Worklog: 2026-04-30 - README Codex startup timeout guidance

## Goal
Refine the README MCP setup guidance so Codex users prefer the compiled host executable and know how to raise MCP startup timeout when Excel startup is slow.

## Planned changes
- update the Codex registration examples to use the built `ExcelMcp.ToolHost.exe`
- add a short note explaining why `dotnet run` is less reliable for MCP startup
- document a representative `startup_timeout_sec` setting

## Changes made
- changed the README Codex registration examples from `dotnet run` to the compiled host executable
- added guidance to prefer the built host for steadier startup times
- added a `config.toml` snippet showing `startup_timeout_sec = 60`

## Findings
- the previous README examples were valid, but they encouraged the slower launch path for MCP registration
- a startup timeout example belongs in the user-facing setup section because it is an operational integration concern rather than deep architecture

## Decisions taken
- recommend the compiled executable as the default Codex setup path in the README
- keep `60` seconds as the documented example timeout without treating it as a required universal value

## Tests
- not run; documentation-only change

## Next
- consider adding a publish-output example later if the repo adopts a preferred packaging path for non-developer workstations
