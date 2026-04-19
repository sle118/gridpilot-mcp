# Live Excel tests

These tests are intended to validate real desktop Excel automation behavior.

## Principles
- Opt-in only
- Not required for normal repository validation
- Not enabled by default in GitHub CI
- Must use disposable workbooks or copied fixtures
- Must restore application state and clean temporary artifacts

## Suggested environment variables
- `RUN_LIVE_EXCEL_TESTS=1`
- `EXCEL_LIVE_TEST_WORKBOOK=<path>`

## Suggested usage
Live tests should skip automatically when the environment is not explicitly configured.
