# Topic: Branding and naming

## Purpose

This document explains the current split between repository branding and starter implementation naming.

## Current decision

The repository identity is **GridPilot MCP**.

The bootstrap C# solution still uses provisional `ExcelMcp.*` project and namespace names. This is intentional for now because the repo was produced as layered zip packs, and a late-stage overlay can safely rewrite human-facing files without creating unzip collisions or leaving broken project references.

## Practical rule

Use **GridPilot MCP** in:

- README and root docs
- AGENTS.md
- CONTRIBUTING.md
- handoff docs
- ADRs and topic docs
- user-facing presentation material

Use existing `ExcelMcp.*` code-level names unchanged until a dedicated rename task is explicitly planned and validated.

## Why not rename immediately

A full rename would touch:

- solution file names
- project directories
- csproj references
- namespaces
- test project names
- documentation references

Doing that before the first working slices are implemented creates avoidable churn and makes overlay-based workspace reconstruction messier.

## Future rename conditions

A dedicated rename pass becomes reasonable once:

- the first implementation slice is stable
- basic tests are in place
- project structure is no longer in flux
- the rename can be done atomically with docs and test updates
