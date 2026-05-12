# Worklog: 2026-05-12 - DEPLOY-011 setup and startup

## Intent

Implement the DEPLOY-011 slice for Windows setup, installed-layout startup registration, and tray startup/profile bootstrap behavior.

## Planned scope

- add deployment-core installation and startup services
- add a dedicated `GridPilot.Setup` WinForms wizard
- add tray startup flags and installed-profile fallback/bootstrap
- update release packaging so setup ships with the public ZIP
- update docs and validation for the installed flow

## Follow-up fixes

- fix setup wizard page layouts so preview and execution text areas use the available vertical space
- remove unnecessary installed-profile working-directory emission to avoid unresolved VS Code / Copilot warnings
- make remaining agent-config working-directory warnings actionable in the tray UI
