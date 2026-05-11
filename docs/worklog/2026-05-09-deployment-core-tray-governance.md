# Worklog: 2026-05-09 - deployment core and tray governance

## Goal

Record the corrected deployment/tray direction as governance documentation without implementing feature code.

## Changes

- Added a deployment governance topic that frames the initiative as **deployment core + tray shell**, not tray-first.
- Captured the shared deployment-core responsibilities and the limited WinForms tray responsibilities.
- Recorded repo-specific constraints around MCP stdout, file-backed logs, proxy/framing lessons, launch profile shape, and smoke-test behavior.
- Added DEPLOY-001 through DEPLOY-011 packet definitions with acceptance criteria and validation steps.
- Linked the new governance note from the topics index.

## Notes

- No feature code was changed.
- The provisional `ExcelMcp.*` project naming remains the recommended code-level default for the shared deployment library until a dedicated rename task exists.

