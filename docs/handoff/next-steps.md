# Next Steps

The bridge now has a broad enough workbook surface that the next work should focus on **safety refinement and the next high-value structure layers**, not on random surface sprawl.

## Immediate Priorities

1. **Strengthen attached-session safety**
   Improve unsafe UI-state detection and refusal reasons now that formatting and worksheet layout mutations are live.
2. **Decide whether leases are enough**
   Revisit whether mutation permissions should remain a simple approval lease or evolve into a stronger coordination model.
3. **Package the next surface wave**
   Prefer focused slices such as validation/conditional-formatting diagnostics, richer workbook layout/protection helpers, or deeper dependency-aware workflows.
4. **Keep runtime logging sharp**
   Continue adjusting log coverage and fields based on real live-workbook regressions.

## Recommended Next Slice

The best next bounded slice is:

- safer attached-session mutation handling for the broadened workbook-polish baseline
- plus one focused post-polish workbook family, likely validation or workbook-layout refinement

Keep broader shared-session coordination redesign out of that same slice.

## Reference

- roadmap: `docs/topics/workbook-surface-roadmap.md`

## Cautions

- do not broaden mutation behavior faster than safety rules
- do not mix a major rename of `ExcelMcp.*` into unrelated feature work
- keep live Excel tests opt-in
- keep COM details isolated behind interfaces
- keep runtime logging separate from MCP stdout and proxy transport traces
