# Architecture overview

GridPilot MCP is a local desktop automation bridge for Microsoft Excel.

The intended architecture separates concerns clearly:

- **Excel desktop** is the execution engine
- **the C# bridge** is the operational control plane
- **the workbook** is the active document/data plane
- **agents** operate through MCP tools exposed by the host

The repository currently contains a starter solution that still uses provisional `ExcelMcp.*` code-level names. Those names are implementation scaffolding, not the preferred repository branding.

## Design intent

The bridge should own:

- session lifecycle and application state scoping
- workbook open/save/close behavior
- workbook structure inventory
- targeted refresh
- Power Query diagnostics via temporary probe queries
- cleanup of temporary artifacts
- structured diagnostics and error normalization

The workbook should not become the place where orchestration or transport logic accumulates.
