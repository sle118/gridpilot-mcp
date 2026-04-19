# Testing Strategy

## Goals

The project should maintain fast default validation while still allowing real Excel-backed verification locally.

## Test tiers

### Tier 1: Unit tests
Purpose:
- Validate orchestration logic
- Validate state handling
- Validate error normalization
- Validate cleanup and naming rules

Characteristics:
- No real Excel
- Use mocks, fakes, or narrow abstractions around Excel surfaces
- Mandatory for most behavior changes

### Tier 2: Integration tests
Purpose:
- Validate bridge service behavior end-to-end against controlled adapters
- Validate tool contract mapping and structured responses

Characteristics:
- No real Excel required by default
- Uses fixtures and test doubles where possible

### Tier 3: Live Excel tests
Purpose:
- Validate real COM behavior with installed desktop Excel
- Validate refresh behavior, query creation, cleanup, and app-state restoration

Characteristics:
- Opt-in only
- Excluded from CI by default
- Uses disposable workbooks or copied fixtures
- Must skip automatically when environment requirements are missing

## Mocking guidance

Avoid mocking raw COM types across the codebase.

Prefer narrow project-defined abstractions such as:
- application session handle
- workbook handle
- query handle
- table handle
- connection handle

This keeps COM translation localized and improves test stability.

## Live test gating

Recommended environment variables:
- `RUN_LIVE_EXCEL_TESTS=1`
- `EXCEL_LIVE_TEST_WORKBOOK=<path>`

Optional additional values may be introduced later for fixture root or cleanup behavior.

## Live test safety rules

- Never run live tests by default in repository CI
- Never mutate user workbooks directly
- Always restore application state explicitly
- Always clean temp queries and temp files in teardown
