# Worklog: 2026-05-13 - Query, connection, graph, and workbook structure slice

## Goal

Implement the next workbook-surface slice for GridPilot MCP:

- query lifecycle
- workbook data connection lifecycle
- named-structure dependency graph workflows
- workbook-level visibility and protection controls

## Scope

- add deployment-neutral core models for query detail, connection detail, workbook structure state, and dependency graphs
- extend the workbook handle and bridge service with query create/rename/delete, connection get/rename/update/delete, dependency graph reads, and workbook visibility/protection operations
- expose the new surface through MCP tools
- add fake/integration coverage for request parsing, safety validation, and graph serialization
- keep connection creation query-owned only

## Notes

- the existing COM adapter already has useful helpers for query lookup, query-table lookup, and query-to-connection resolution, so the new slice should reuse those paths instead of inventing parallel metadata plumbing
- the dependency graph should stay graph-first and intentionally shallow; this slice is about workbook metadata relationships, not full formula lineage
- workbook protection needs structured failure mapping for password-required and invalid-password cases
