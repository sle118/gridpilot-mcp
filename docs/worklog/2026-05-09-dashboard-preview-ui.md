# 2026-05-09 - DEPLOY-009 Dashboard And Preview UI

## Intent

Implement the next deployment/tray slice as a richer optional WinForms dashboard over the existing `ExcelMcp.Deployment` services.

## Scope

- Add a tabbed dashboard for overview, agent config preview, doctor results, smoke-test results, and recent logs.
- Keep deployment logic in `ExcelMcp.Deployment`; the tray remains lifecycle, clipboard, and async UI orchestration.
- Do not add config writers, startup registration, packaging, or MCP/Excel behavior changes.

## Validation Plan

- Unit-test tray-side presenter and formatter helpers.
- Build the tray project.
- Run normal unit validation where build outputs are not locked.
