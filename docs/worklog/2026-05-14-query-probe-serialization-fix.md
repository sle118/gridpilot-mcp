# Worklog: 2026-05-14 - query probe serialization fix

## Goal
Fix the `query_run_probe` MCP surface so the probe preview serializes cleanly through MCP without exposing the unsupported `object[,]` shape.

## Notes
- The live diagnostics rerun showed `query_run_probe` completing the workbook action and then failing during result serialization.
- The probe preview currently crosses the MCP boundary as `RangeData`, which carries a multidimensional `object?[,]` matrix.
- The likely fix is to convert the probe preview to a JSON-friendly result shape already used elsewhere in the bridge.
