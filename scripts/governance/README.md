# Governance Scripts

Utilities in this folder support repository governance and cross-agent coordination.

## ChatGPT export utility

Use `export_chatgpt_context.py` to create a zip export in the git-ignored `.tmp/chatgpt-exports/` folder.

Examples:

```powershell
python scripts/governance/export_chatgpt_context.py --mode docs
python scripts/governance/export_chatgpt_context.py --mode docs-and-code
```

The script prints the generated archive path after each run.
