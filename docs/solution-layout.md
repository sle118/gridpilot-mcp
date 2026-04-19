# Solution layout

## Separation of concerns

`ExcelMcp.Core` should remain free of COM and host-specific details.

`ExcelMcp.Bridge` should contain orchestration, safety policies, temp-query behavior, state-scoping, and tool-level service logic.

`ExcelMcp.ComAdapter` should be the only place where Excel automation implementation details live.

`ExcelMcp.ToolHost` should wire transport, dependency injection, and tool registration.

## Expected next steps

1. Choose MCP host library / transport strategy.
2. Implement COM-backed `IExcelSession` and `IWorkbookHandle`.
3. Add structured exception mapping.
4. Add session-state scope helper.
5. Expand unit coverage before live automation work.
