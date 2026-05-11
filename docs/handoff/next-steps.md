# Next Steps

The bridge now has a broad enough workbook surface that MCP tool expansion should pause while the project focuses on **deployment core + tray shell** work.

## Immediate Priorities

1. **Validate the public release flow**
   Run the new tag-driven packaging and GitHub publish path end to end, then confirm a separate machine can download the ZIP or clone the GitHub mirror and build locally.
2. **Review DEPLOY-010**
   DEPLOY-010 should still add conservative config writing only after preview/copy behavior is solid.
3. **Preserve deployment-core layering**
   Reuse the existing `ExcelMcp.ToolProxy` / `McpFrameSniffer` lessons, preserve framed and raw JSON-RPC stdio support, keep runtime logs file-backed, and keep MCP stdout JSON-RPC only.

## Recommended Next Slice

After the public release flow is validated, the best next bounded slice is:

- DEPLOY-010 optional config writers
- keep installer/startup registration separate from the portable ZIP release path until DEPLOY-011

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
