# ADR-0002: Lightweight repository governance and project-memory layout

## Status
Accepted

## Context

The project is expected to involve multiple agents and multiple work sessions. Without a small but explicit documentation standard, context drifts and handoff quality degrades.

## Decision

Adopt a lightweight documentation structure with:
- root operational guidance in `AGENTS.md`
- durable architecture in `docs/architecture/`
- formal decisions in `docs/decisions/`
- focused technical analysis in `docs/topics/`
- dated session records in `docs/worklog/`
- compact handoff state in `docs/handoff/`

Narrative is allowed, but it should live below the main operational surface.

## Consequences

### Positive
- Faster agent onboarding
- Clear separation between durable design and session history
- Better cross-agent continuity
- Lower context noise in root documents

### Ongoing obligations
- Worklogs must be maintained
- Handoff docs must be updated when state or priorities change
- Behavioral or architectural changes should update tests and docs together

## Related
- `AGENTS.md`
- `CONTRIBUTING.md`
