# Worklog: 2026-05-11 - SonarLint JRE workspace fix

## Goal

Stop the SonarLint language server from crashing in this workspace by giving it a known-good local Java runtime.

## Changes

- Added a workspace-level VS Code setting for `sonarlint.ls.javaHome`.
- Pointed SonarLint at the installed JDK 21 on this machine.

## Notes

- This is a local workspace fix, not a repo-wide product change.
- No application code, tests, or documentation guidance changed beyond the worklog entry.
