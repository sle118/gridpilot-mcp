# ADR-0001: Local C# MCP bridge for live Excel desktop automation

## Status
Accepted

## Context

The project needs richer workbook and Power Query control than a workbook-embedded VBA bus can comfortably provide. The workbook should not carry orchestration, diagnostics, retries, and transport responsibilities.

## Decision

Use a local C# MCP bridge to control a live desktop Excel instance through the Excel object model.

Keep VBA, if present, only as a temporary bootstrap or fallback path.

## Consequences

### Positive
- Richer access to Excel application and workbook surfaces
- No worksheet cell-payload bottleneck for bridge communication
- Better centralization of diagnostics, cleanup, and state restoration
- Better fit for structured tool contracts

### Negative or constraining
- The target remains local interactive desktop automation
- COM lifetime and state handling must be engineered carefully
- Live Excel tests are harder to automate at repository CI level

## Related
- `docs/architecture/overview.md`
- `docs/topics/power-query-diagnostics.md`
