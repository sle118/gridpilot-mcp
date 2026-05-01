# Worklog: 2026-05-01 - repo-wide mutation permission model

## Goal
Replace the attached-only workbook approval lease with a unified mutation-permission model that can cover one workbook or any workbook for the current GridPilot host session.

## Planned changes
- replace the attached-only approval registry/service with a generic mutation-permission registry/service
- enforce mutation permission for all mutating workbook operations, not just attached-session ones
- preserve attached-session owner-targeting and unsafe-UI safety checks on top of the broader permission model
- add explicit MCP tools for granting, revoking, and reading workbook-scoped and session-scoped mutation permission
- expose effective mutation permission state on connection responses so clients can suppress repeated prompts
- keep the existing attached-session grant/revoke tools as compatibility shims for workbook-scoped permission

## Notes before implementation
- current repeated prompts seen in live bridge-owned workflows are not explained by the existing in-repo attached approval model, because bridge-owned connections currently report `approvalState: "not_applicable"`
- the repo can still align the mutation-permission surface and state model so the client has one clear place to bind “approve this workbook” and “approve this session”

## Changes made
- added a generic mutation-permission model with:
  - workbook-scoped permission
  - session-scoped permission
  - host session id
  - grant/revoke/status result models
- updated the host resolver and connection surface to project effective mutation permission state alongside the legacy approval aliases
- added generic MCP tools for mutation permission grant, revoke, and status, while keeping the attached-session grant/revoke tools as compatibility shims
- updated bridge safety to evaluate generic mutation permission for mutating operations while preserving attached-session owner-targeting and UI safety checks
- kept attached-session refusal codes for attached flows, while bridge-owned/create-new flows now use generic mutation-permission refusal codes

## Findings
- the repo can now express the permission scopes the client needs, but this alone does not guarantee that the Codex/MCP client will stop prompting per mutating tool call
- maintaining the legacy attached approval aliases reduces churn for existing attached-session tests and documentation while the client-facing model broadens
