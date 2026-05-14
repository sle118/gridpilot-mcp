# Agent-Driven GridPilot Validation Guide

## Purpose

Use this guide when a live agent needs to validate **GridPilot itself as a product surface**, not just run the repo's live xUnit harness.

This is the extra layer that answers questions like:

- can setup install the product correctly?
- can the tray launch and expose the expected actions?
- can an MCP client register the host successfully?
- can the agent actually connect to a workbook and drive GridPilot tools end to end?
- do product-facing issues show up that the live test harness would not catch?
- can a weaker agent recover from attach/session failures using GridPilot's own guidance and diagnosis surfaces?

This guide complements, but does not replace:

- `docs/topics/agent-live-testing-guide.md`

## Read First

Read these before starting:

- `docs/handoff/current-state.md`
- `docs/handoff/next-steps.md`
- `docs/topics/mcp-setup-and-troubleshooting.md`
- `docs/topics/public-distribution-and-release-workflow.md`
- `docs/topics/agent-live-testing-guide.md`

If the validation is attached-session or workbook-owner specific, also read:

- `docs/architecture/shared-session-safety.md`
- `docs/topics/codex-live-attach-troubleshooting-prompt.md`

## When To Use This Guide

Use this guide when the task is primarily about:

- setup wizard behavior
- tray dashboard behavior
- VS Code / Copilot registration behavior
- MCP startup behavior from the real packaged app
- end-to-end workbook operations through a real MCP client
- release-validation passes on another workstation

Do not use this guide as a substitute for:

- unit or integration tests
- the opt-in live xUnit harness

## Validation Modes

Choose one mode before you start:

1. **Installed release validation**
   Use a GitHub release ZIP or other packaged payload.
2. **Local source-build validation**
   Use local `bin/Release` or `bin/Debug` outputs from the repo.

If the task is about public usability, prefer installed release validation.

## Environment Contract

Assume and verify:

- Windows workstation
- desktop Excel installed
- MCP-capable client available
  - Codex preferred if that is the target client
  - VS Code / GitHub Copilot if the task is client-compatibility specific
- no important user workbooks open
- test workbook is disposable

Do not:

- drive GridPilot against a user's real workbook
- reuse a contaminated Excel session without noting it
- overwrite a user MCP config without preview/backup when the flow is optional

## Recommended Product-Level Run Order

Unless the task is narrower, use this order:

1. acquire payload
2. install or launch the product
3. verify tray/setup basics
4. register the MCP host with the chosen client
5. connect to a disposable workbook
6. run a compact workbook-operation checklist
7. use the diagnosis surface intentionally if something fails
8. capture findings

## Payload Acquisition

### Installed release

1. Download the target `gridpilot-mcp-vX.Y.Z-windows-x64.zip`.
2. Unzip into a disposable folder.
3. Record the exact tag or test release version.

Expected top-level entry points:

- `GridPilot.Setup.exe`
- `GridPilot.Tray.exe`
- `host\\ExcelMcp.ToolHost.exe`
- `proxy\\ExcelMcp.ToolProxy.exe`

### Local source build

Run:

```powershell
dotnet build ExcelMcp.sln -c Release
```

Then record the exact commit being tested.

## Setup And Tray Validation

### Setup path

If validating setup:

1. Launch `GridPilot.Setup.exe`.
2. Choose per-user or machine-wide scope deliberately.
3. Review the preview/actions page carefully.
4. Complete install.
5. Record:
   - install scope
   - install path
   - startup registration choice
   - whether elevation was required

Validate at minimum:

- version text is visible where expected
- preview text and execution/status text are readable
- install/update/repair/uninstall behavior is coherent for the chosen scenario

### Tray path

Launch `GridPilot.Tray.exe`.

Validate at minimum:

- tray icon appears with branding
- taskbar/window icon is branded where applicable
- dashboard opens correctly
- overview details are readable
- agents preview pane is readable
- doctor results are readable
- smoke test results are readable
- about dialog shows branding and version information

If testing installed layout specifically, prefer launching the installed tray path instead of the extracted portable path.

## MCP Client Registration

For Codex, use the host registration flow from:

- `docs/topics/mcp-setup-and-troubleshooting.md`

Typical attached workbook-owner registration:

```powershell
codex mcp add gridpilot -- C:\path\to\ExcelMcp.ToolHost.exe --session-mode attach --attach-target workbook-owner
```

Typical dedicated hidden Excel instance:

```powershell
codex mcp add gridpilot -- C:\path\to\ExcelMcp.ToolHost.exe --session-mode create-new
```

Useful checks:

```powershell
codex mcp list
codex mcp get gridpilot
```

If troubleshooting transport or startup:

- register through `ExcelMcp.ToolProxy.exe`
- enable runtime file logging

After the host is registered, remember that many workbook-targeted results now echo:

- `guidance.targetContext`
- `guidance.recommendedNextTools`
- `guidance.workflowHints`

If the client drifts after a planning turn, look for `guidance.targetContext.connectionId` and reuse it instead of reconnecting blindly.

## Disposable Workbook Rules

Use a disposable workbook only.

Recommended sources:

- copied fixture from `tests/live/fixtures/test_workbook.xlsx`
- a scratch workbook created specifically for the validation pass

Record which workbook path you used.

## Minimum End-To-End Tool Checklist

Once the MCP host is registered and reachable, drive this compact checklist through the client.

### Session and connection

1. `session_list_open_workbooks`
2. `session_connect_workbook`
3. `workbook_list_inventory`

Record:

- whether the workbook was discovered cleanly
- whether attach succeeded
- whether inventory returned expected sheets/queries/connections/tables

### Basic read path

Run at least one:

- `range_read`
- `name_read`
- `table_read`
- `query_get` or `query_get_detail`

### Mutation path

Run at least one safe mutation that fits the task:

- `range_write`
- `range_set_formulas`
- `query_set_formula`
- `name_create` or `name_update`
- `worksheet_create`

If the slice being validated is workbook-lifecycle specific, include:

- `query_create`
- `query_rename`
- `query_delete`
- `connection_get`
- `connection_update`
- `workbook_get_dependency_graph`
- `workbook_get_structure_state`
- `workbook_set_visibility`
- `workbook_set_protection`

### Diagnostics path

Run at least one:

- tray doctor
- tray smoke test
- `calculation_inspect_errors`
- `query_run_probe`

For attached-session or real-client troubleshooting, also run:

- `session_get_diagnostics`
- `diagnostics_get_runtime`
- `diagnostics_read_log_tail`
- `diagnostics_build_report`

## Diagnosis-First Recovery Workflow

If the agent sees attach instability, stale session behavior, or opaque COM failures such as `RPC_E_DISCONNECTED`, do not stop at the first failure. Use GridPilot's diagnosis tools before cleanup.

Recommended order:

1. record the exact failing tool call
2. re-use the same `connectionId` if one was already returned
3. call `session_get_diagnostics`
4. call `diagnostics_get_runtime`
5. if the current log level is too low, call `diagnostics_set_log_level` with `trace`
6. rerun the failing step once
7. call `diagnostics_read_log_tail`
8. call `diagnostics_build_report` with recent tails included
9. restore log level to `default` after the run

Example diagnosis calls:

```json
{ "level": "trace", "scope": "both" }
```

```json
{ "kind": "runtime", "maxLines": 120 }
```

```json
{ "connectionId": "<your connection id>", "includeRecentLogTails": true }
```

## Optional VS Code / Copilot Validation

If the task involves VS Code or GitHub Copilot:

1. use the tray action or manual preview path for the VS Code user `mcp.json` writer
2. confirm backup behavior if a real write is performed
3. restart or refresh the client if needed
4. verify the GridPilot server appears and tools are discoverable
5. note any manifest or schema compatibility errors exactly

If the client crashes or rejects the manifest, capture the exact error text, not a paraphrase.

## Failure Classification

When a problem appears, classify it as one of:

- **setup/install**
- **tray UX**
- **host startup**
- **MCP transport**
- **client registration**
- **workbook attach/routing**
- **tool behavior**
- **diagnosis/guidance surface**
- **environment contamination**
- **unknown**

That classification should appear in the final report.

## Cleanup

After the run:

- disconnect the workbook session if appropriate
- close GridPilot tray instances opened for the test
- close Excel instances opened for the test
- remove temporary workbook copies if they are not needed for investigation
- note whether the machine was left clean

## Reporting Template

Use this format when reporting back:

```md
# Agent-Driven GridPilot Validation Report

## Scope
- installed release validation / local source-build validation
- target slice or bug

## Environment
- machine:
- Windows version:
- Excel version:
- client:
- GridPilot version or commit:
- workbook used:

## Entry Path
- setup used: yes/no
- tray used: yes/no
- host path:
- proxy used: yes/no

## Registration
- command:
- result:

## Product Checks
- setup:
- tray:
- version display:
- icons/branding:

## MCP Drive Results
- session discovery:
- workbook connect:
- inventory:
- read path:
- mutation path:
- diagnostics path:
- guidance/remediation help:

## Failures
1. Category:
   Step:
   Exact error:
   Notes:

## Diagnosis Artifacts
- session diagnostics:
- runtime diagnostics:
- log tail captured:
- diagnostic report captured:

## Cleanup
- workbook cleanup:
- Excel cleanup:
- process cleanup:

## Recommended Next Step
1. ...
```

## Short Handoff Prompt

Use this when asking another live agent to do a product-level pass:

```text
Run a product-level GridPilot validation pass using docs/topics/agent-driven-gridpilot-validation-guide.md.

Scope:
- <insert target release, slice, or bug>

Required output:
- completed Agent-Driven GridPilot Validation Report
- exact MCP registration commands
- exact tool calls or dashboard actions exercised
- exact failure text for any issue
```
