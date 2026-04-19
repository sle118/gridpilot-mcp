# ADR-0003: Use GridPilot MCP branding while retaining provisional ExcelMcp code names

## Status

Accepted

## Context

The repository was bootstrapped through layered generated packs. A later branding package introduced the project identity **GridPilot MCP**, while the existing governance and C# skeleton packs already used `ExcelMcp.*` names internally.

A full rename of solution, project, and namespace identifiers would be possible, but doing it immediately would introduce unnecessary churn before the first real implementation slices exist.

## Decision

The repository will use **GridPilot MCP** as its human-facing identity.

The starter code will retain provisional `ExcelMcp.*` solution, project, and namespace names until a dedicated tracked rename task is scheduled.

## Consequences

### Positive

- clean unzip overlay path for workspace reconstruction
- lower churn during early implementation
- branding is visible immediately in repository-facing materials
- agents get an explicit rule instead of guessing

### Negative

- temporary mismatch between repo branding and code-level names
- later rename task will still be required if code-level branding is to match

## Related

- `docs/topics/branding-and-naming.md`
- `docs/handoff/current-state.md`
