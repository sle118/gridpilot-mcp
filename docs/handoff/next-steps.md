# Next Steps

The bridge now has a broad enough workbook surface that MCP tool expansion should pause while the project focuses on **deployment core + tray shell** work.

## Immediate Priorities

1. **Review DEPLOY-009**
   Confirm the dashboard remains a thin optional WinForms surface over `ExcelMcp.Deployment` for profile, config preview, log, doctor, diagnostic, and smoke-test behavior.
2. **Plan optional config writers**
   DEPLOY-010 should add conservative config writing only after preview/copy behavior is solid.
3. **Preserve deployment-core layering**
   Reuse the existing `ExcelMcp.ToolProxy` / `McpFrameSniffer` lessons, preserve framed and raw JSON-RPC stdio support, keep runtime logs file-backed, and keep MCP stdout JSON-RPC only.

## Recommended Next Slice

After DEPLOY-009 is reviewed, the best next bounded slice is:

- DEPLOY-010 optional config writers
- keep startup registration and packaging out until DEPLOY-011

Config writers must stay conservative: preview diffs, back up existing files, avoid blind overwrites, support dry-run, and report exact modified paths.

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
