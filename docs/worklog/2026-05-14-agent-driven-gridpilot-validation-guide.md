# Worklog: 2026-05-14 - agent-driven GridPilot validation guide

## Goal

Add a separate repeatable guide for live agents to drive GridPilot itself through setup, tray, MCP registration, workbook connection, and representative tool calls.

## Scope

- define the product-level validation workflow distinct from the live xUnit harness
- include source-build and installed-release entry paths
- include concrete MCP registration and workbook-driving steps
- include a reporting template for agent-driven validation

## Notes

- the existing live-testing guide is still useful for the repo harness
- this new companion doc covers the missing "act like a user and actually drive GridPilot" layer
