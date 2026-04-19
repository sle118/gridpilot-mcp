# Topic: Power Query diagnostics

## Purpose

Capture the current diagnostic strategy for troubleshooting workbook queries through the bridge.

## Current understanding

Excel exposes workbook queries and related refresh surfaces, but it does not expose the Power Query editor's step-by-step preview experience in a way that directly solves troubleshooting.

The preferred bridge pattern is to create temporary diagnostic or probe queries derived from a target query formula, run them, capture structured output or failure, and remove them afterward.

## Core pattern

1. Resolve the target query
2. Read its formula
3. Create a temporary query for a diagnostic or probe purpose
4. Refresh the temporary query or associated output surface
5. Capture result preview or structured failure information
6. Remove the temporary query
7. Provide a cleanup sweep for abandoned temp artifacts

## Near-term tooling direction

Expected early bridge operations include:
- targeted query run attempt
- targeted connection refresh
- temp query probe execution
- cleanup of temp query artifacts
- wait for async query completion

## Notes on step-by-step analysis

A dedicated `preview_query_steps` helper may be useful later, but it does not need to be part of the first implementation wave.

Initially, agents can drive step isolation strategically by creating a sequence of temporary probe queries against intermediate formula states.

## Open questions

- What result schema should probe operations return?
- How should temp query naming and ownership be tracked?
- Which table-backed refresh paths need dedicated handling?
