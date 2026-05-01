# Worklog: 2026-04-30 - MCP proxy diagnostics

## Goal
Add a dedicated MCP stdio proxy so Codex-to-host startup and handshake behavior can be captured directly instead of debugging by repeated host changes.

## Planned changes
- add a small console proxy project that launches the real MCP host and forwards stdin/stdout/stderr unchanged
- log MCP request and response framing with timestamps to a file
- document how to register Codex against the proxy and where to inspect logs
- add focused tests for proxy message parsing

## Changes made
- added a standalone `ExcelMcp.ToolProxy` console project that wraps a stdio MCP command and logs framed MCP traffic to a file
- added focused integration coverage for MCP frame parsing across chunk boundaries and both supported header terminators
- documented Codex registration against the proxy in the README
- tightened proxy diagnostics to log per-chunk previews, parser state after each chunk, and parser exceptions so partial or nonstandard Codex startup traffic can be identified from a single repro
- extended the proxy sniffer to recognize both framed MCP messages and raw JSON messages so startup handshakes can be logged end-to-end regardless of transport style

## Findings
- direct MCP probing shows the host responds correctly to `initialize` and `tools/list`
- the remaining uncertainty is now in the Codex-to-child-process boundary, so a transparent proxy is the fastest way to get ground truth
- Codex is writing to the proxy immediately during startup, but the current log is still ambiguous about whether that first chunk is a complete MCP frame, a partial frame, or a non-MCP preamble
- the upgraded proxy log shows Codex is sending a bare JSON `initialize` request on stdio without `Content-Length` framing, so the host was waiting forever for MCP headers that never arrived
- the next proxy log shows Codex closes the connection immediately after the host writes a framed `Content-Length` initialize response, which indicates Codex expects the response transport to mirror its raw JSON request style
- the MCP 2025-06-18 stdio transport expectation is newline-delimited JSON-RPC rather than LSP-style `Content-Length` framing, so raw stdio mode should emit one compact JSON object per line and flush immediately

## Decisions taken
- keep the proxy generic enough to wrap any stdio MCP command
- log parsed MCP frames plus raw stderr lines rather than only process lifecycle events
- make the host stdio reader tolerant of both framed MCP messages and headerless raw JSON-RPC objects so Codex startup can succeed without a custom wrapper
- make stdio responses symmetric with the detected request transport so raw JSON request mode returns raw JSON responses instead of framed MCP output
- align raw stdio responses with newline-delimited JSON-RPC by appending `\n` after each raw response while still tolerating either newline-delimited or brace-balanced raw input on reads

## Tests
- `dotnet build ExcelMcp.sln -nologo`
- `dotnet test tests/ExcelMcp.IntegrationTests/ExcelMcp.IntegrationTests.csproj -nologo`

## Next
- rerun Codex against the upgraded proxy and inspect the first stdin chunk preview plus parser state to determine whether the timeout is caused by partial framing, an unexpected preamble, or a downstream host response issue
