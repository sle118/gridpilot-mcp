# Worklog: 2026-05-01 - live attach owner resolution

## Goal
Continue the live attach troubleshooting pass by comparing the identity path used by running-workbook discovery versus workbook-owner attachment, then make the smallest attach-side fix supported by the current runtime logs and live repro.

## Planned checks
- reproduce `session_list_open_workbooks`, `session_connect_workbook`, and `workbook_list_inventory` against the real registered GridPilot host
- inspect `.tmp/gridpilot-runtime.log` after the repro
- compare `ListRunningWorkbooks` and `FindWorkbookOwnerApplications` in `RunningWorkbookObjectTable`
- keep scope limited to attach-side owner resolution and focused test coverage

## Findings
- the real registered host still lists the live workbook as `https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx`
- the same exact workbook path still fails attach and inventory with `attach_target_no_matching_instance`
- `ListRunningWorkbooks` performs workbook identity discovery inside `StaOperationRunner`, while `FindWorkbookOwnerApplications` still performs its identity walk directly on the caller thread
- that means the attach path is not using the same apartment/construction path as discovery even though both rely on the same ROT workbook identity rules
- after aligning workbook-owner match discovery with the STA path, debug logging showed `matchingMonikerCount=1` and `matchCount=0`, which proved the failure had moved from identity matching to caller-thread re-resolution of the matched ROT entry
- replacing caller-thread ROT re-resolution with a raw `IUnknown` handoff proved the owner application was being found, but then failed with `RPC_E_WRONG_THREAD`, which confirmed the remaining issue was COM cross-thread transfer rather than workbook identity comparison
- using COM inter-thread interface marshaling for the matched Excel `Application` object fixed the remaining attach-side transfer bug: live attach connect and live attached inventory now succeed for the HTTPS workbook identity

## Changes made
- kept workbook-owner identity matching on the same STA discovery path used by `ListRunningWorkbooks`
- split workbook-moniker display-name normalization into a shared helper so discovery and owner targeting use the same candidate-path cleanup rules
- changed workbook-owner application transfer from caller-thread ROT re-resolution to COM inter-thread marshaling via `CoMarshalInterThreadInterfaceInStream` / `CoGetInterfaceAndReleaseStream`
- added focused unit coverage for shared workbook-moniker candidate normalization

## Validation
- `dotnet test tests/ExcelMcp.UnitTests/ExcelMcp.UnitTests.csproj -nologo --filter RunningWorkbookObjectTableTests`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo --filter WorkbookServiceResolverTests`
- manual raw-JSON host repro against `src/ExcelMcp.ToolHost/bin/Debug/net8.0/ExcelMcp.ToolHost.exe` with:
  - `session_list_open_workbooks` => returns `https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx`
  - `session_connect_workbook` => succeeds in attached `workbook-owner` mode
  - `workbook_list_inventory` => succeeds for the same workbook path

## Next
- rerun through the external registered GridPilot connector after it reconnects, because I had to stop the old locked `ExcelMcp.ToolHost` process during rebuild
