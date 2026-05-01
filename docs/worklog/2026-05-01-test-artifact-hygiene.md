# Worklog: 2026-05-01 - test artifact hygiene

## Goal
Keep the new live workbook artifact without leaving ad hoc Excel files at the repo root, and make the commit-vs-ignore guidance explicit in the repository itself.

## Changes made
- moved `Book_mcp_test.xlsx` from the repo root into `tests/live/fixtures/Book_mcp_test.xlsx`
- updated `.gitignore` to ignore a root-level `Book_mcp_test.xlsx` so accidental local re-creation does not show up as repo noise
- updated `tests/live/fixtures/README.md` to document `Book_mcp_test.xlsx` as an auxiliary tracked live fixture artifact rather than the default baseline workbook

## Rationale
- the repo already has an established tracked live-fixture location under `tests/live/fixtures/`
- keeping workbook artifacts at the repo root makes status noisier and blurs the distinction between durable fixtures and local scratch files
- this preserves the workbook as a real test artifact while keeping the repo layout aligned with the existing live-test harness

## Validation
- confirmed `Book_mcp_test.xlsx` now lives under `tests/live/fixtures/`
- confirmed `.gitignore` now blocks a future root-level copy from being reintroduced accidentally
