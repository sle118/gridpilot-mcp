# Next Steps

The bridge now has a broad enough workbook surface that MCP tool expansion should pause while the project focuses on **deployment core + tray shell** work.

## Immediate Priorities

1. **Validate DEPLOY-011 manually**
   Run per-user install, machine-wide install, startup enable/disable, update, repair, and uninstall passes from the public ZIP on a separate Windows machine.
2. **Validate the tray DEPLOY-010 action manually**
   Verify the new tray preview-and-write flow against real `%APPDATA%\\Code\\User\\mcp.json` content, including unrelated MCP servers, malformed JSON, dry-run preview, backup creation, and already-matching configs.
3. **Validate the Copilot compatibility manifest path manually**
   Confirm that the VS Code / GitHub Copilot client receives the conservative no-array-input manifest for the affected tools and can invoke the array-heavy write operations through the string-encoded JSON compatibility path.
4. **Preserve deployment-core layering**
   Reuse the existing `ExcelMcp.ToolProxy` / `McpFrameSniffer` lessons, preserve framed and raw JSON-RPC stdio support, keep runtime logs file-backed, and keep MCP stdout JSON-RPC only.

## Recommended Next Slice

After the public release flow is validated, the best next bounded slice is:

- setup-side opt-in wiring for the DEPLOY-010 VS Code user config writer
- keep install/startup behavior conservative and user-visible while config writing remains preview-first and never automatic

Config-writing actions must stay conservative: preview diffs, back up existing files, avoid blind overwrites, support dry-run, and report exact modified paths.

## Reference

- deployment governance: `docs/topics/deployment-core-and-tray-governance.md`
- deployment inventory: `docs/topics/deployment-inventory-and-current-surface.md`
- previous workbook roadmap: `docs/topics/workbook-surface-roadmap.md`

## Cautions

- do not broaden mutation behavior faster than safety rules
- do not mix a major rename of `ExcelMcp.*` into unrelated feature work
- keep live Excel tests opt-in
- keep COM details isolated behind interfaces
- keep runtime logging separate from MCP stdout and proxy transport traces
- do not put deployment-core behavior directly in the future tray project
- keep the GitLab release jobs pinned to a Windows runner tag so provisioning stays deterministic
