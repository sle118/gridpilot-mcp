# Current State

GridPilot MCP is now a **working local C# MCP bridge for live desktop Excel**, not just a repo skeleton. The current baseline already covers the major workbook operation families needed for real agent workflows.

## In Place Today

- **Workbook targeting and session control**
  - lazy MCP startup
  - open-workbook discovery
  - connect, create, list/get/disconnect connections
  - connection-aware routing and save-as retargeting
- **Workbook and worksheet structure**
  - inventory for sheets, tables, queries, and connections
  - save and save-as
  - worksheet create, rename, delete, move, copy, reorder, and visibility control
  - workbook dependency graph reads
  - workbook visibility read/set and workbook protection read/set
- **Range operations**
  - read/write values
  - read/write formulas
  - clear contents
  - compact range formatting read/write
  - row height, column width, and autofit
- **Query, table, and name surfaces**
  - query read/detail, create, rename, delete, targeted refresh, probe, cleanup, and formula update
  - workbook data connection read/detail, rename, update, and delete
  - table read/detail/create/resize/append/replace/delete/options
  - workbook and worksheet-scoped name list/read/create/update/delete
- **Diagnostics and safety**
  - workbook/worksheet/range recalculation
  - compact formula and literal error inspection
  - attached-session mutation permission leases
  - structured runtime logging across host, bridge, and COM adapter
  - MCP-first session and runtime diagnostics, including log discovery, bounded log tails, redacted diagnostic reports, and runtime/persistent log-level control
  - guidance-first MCP result enrichment with sticky target context, recommended next tools, and remediation hints for common attach/target failures
- **Validation**
  - unit and integration coverage for the implemented surface
  - opt-in live Excel harness including attached-session checks
  - portable Windows release ZIPs and a GitHub public mirror for external consumption
  - a dedicated `GridPilot.Setup` WinForms installer for per-user and machine-wide installs
- deployment-core preview and conservative write support for the VS Code / GitHub Copilot user `mcp.json` file
- an explicit tray action to preview and write the VS Code / GitHub Copilot user `mcp.json` file from an installed tray instance
- a VS Code / GitHub Copilot compatibility schema profile for array-heavy MCP tools, allowing the host to expose safer string-encoded JSON inputs to fragile clients while preserving the richer default schemas for other MCP clients
- host-owned runtime diagnostics settings persisted under the GridPilot user profile rather than in MCP client config
- Windows GitLab CI jobs are now expected to run on a tagged `windows-release` runner VM

## Naming Note

Use **GridPilot MCP** in repo-facing material.

The code still uses `ExcelMcp.*` names intentionally until a dedicated rename pass is planned.

## Stable Direction

- local interactive Excel desktop automation only
- out-of-process C# bridge as the control plane
- workbook stays the data plane
- mock-first validation with opt-in live Excel coverage
- explicit workbook connection after MCP startup
- eventual safe coexistence between human editing and agent operations

## Main Gaps

- attached-session unsafe-UI detection is still narrower than the broadened mutation surface
- coordination between human editing and agent editing is still lease-based rather than fully modeled
- validation and conditional-formatting surfaces are not implemented yet
- generic standalone external connection creation is still intentionally out of scope; new workbook connections remain query-owned
- the new setup/install flow still needs manual validation across more Windows environments and update paths
- setup still does not invoke the VS Code user-config writer automatically
- GitHub Copilot / VS Code MCP compatibility still needs end-to-end validation on the public prerelease path, especially for array-heavy write tools
- the new guidance-first and diagnosis-first surface still needs live validation against real attached-session failures such as `RPC_E_DISCONNECTED`
- the public release flow is GitHub-based and intentionally lighter weight than a dedicated website or package-manager channel
- the GitLab release pipeline requires a Windows runner VM rather than a Linux-only runner
