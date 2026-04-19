# Worklog: 2026-04-19 - ChatGPT export utility

## Goal
Add a small governance utility that can export docs-only or docs-plus-code context into a git-ignored temp location for coordination with ChatGPT.

## Changes made
- Added `scripts/governance/export_chatgpt_context.py` to create zip exports for `docs` or `docs-and-code` modes
- Added `scripts/governance/README.md` with minimal usage examples
- Added `.tmp/` to `.gitignore` so all generated coordination exports stay out of version control
- Verified that both export modes produce unique archive names under `.tmp/chatgpt-exports/`

## Findings
- A zip-based export is simpler for handoff than a copied directory because it preserves relative paths and produces a single artifact per run
- Keeping exports under a repo-local ignored temp folder makes the workflow easy for agents without risking accidental commits

## Decisions taken
- Use a dedicated governance scripts folder rather than putting ad hoc helper scripts at the repo root
- Export only whitelisted repo content and always exclude `.git`, `.env`, build outputs, and previous temp exports
- Generate unique archive names with a UTC timestamp plus a short random suffix

## Tests
- `python scripts/governance/export_chatgpt_context.py --mode docs`
- `python scripts/governance/export_chatgpt_context.py --mode docs-and-code`

## Next
- Use the exporter when packaging repo context for ChatGPT coordination, and extend the whitelist only if future workflows need more files.
