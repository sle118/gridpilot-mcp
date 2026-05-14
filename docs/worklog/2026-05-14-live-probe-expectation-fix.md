# Worklog: 2026-05-14 - live probe expectation fix

## Goal
Update the live probe assertion to match the current known-error workbook fixture output.

## Notes
- The teardown hardening removed the COM disconnect failures.
- The remaining live failure is an expectation mismatch in `LiveProbeTests`.
- Prefer a stable assertion on the probe preview content rather than the previous hard-coded label.
