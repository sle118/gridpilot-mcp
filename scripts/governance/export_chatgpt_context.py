from __future__ import annotations

import argparse
from datetime import datetime, UTC
from pathlib import Path
import sys
import uuid
import zipfile


REPO_ROOT = Path(__file__).resolve().parents[2]
EXPORT_ROOT = REPO_ROOT / ".tmp" / "chatgpt-exports"

DOC_FILES = [
    "AGENTS.md",
    "README.md",
    "CONTRIBUTING.md",
]

DOC_PATHS = [
    "docs",
]

CODE_FILES = [
    ".gitignore",
    "Directory.Build.props",
    "Directory.Build.targets",
    "ExcelMcp.sln",
]

CODE_PATHS = [
    "src",
    "tests",
]

ALWAYS_EXCLUDED_PARTS = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "TestResults",
    "artifacts",
    ".tmp",
}

ALWAYS_EXCLUDED_FILES = {
    ".env",
    ".env.local",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export repo context for ChatGPT coordination into a git-ignored temp zip."
    )
    parser.add_argument(
        "--mode",
        choices=["docs", "docs-and-code"],
        default="docs",
        help="Export docs only or docs plus source/test code.",
    )
    return parser.parse_args()


def should_exclude(path: Path) -> bool:
    if path.name in ALWAYS_EXCLUDED_FILES:
        return True

    return any(part in ALWAYS_EXCLUDED_PARTS for part in path.parts)


def iter_files(paths: list[Path]) -> list[Path]:
    collected: list[Path] = []

    for path in paths:
        if not path.exists():
            continue

        if path.is_file():
            if not should_exclude(path.relative_to(REPO_ROOT)):
                collected.append(path)
            continue

        for candidate in path.rglob("*"):
            if candidate.is_dir():
                continue

            relative = candidate.relative_to(REPO_ROOT)
            if should_exclude(relative):
                continue

            collected.append(candidate)

    return sorted(set(collected))


def build_file_list(mode: str) -> list[Path]:
    selected = [REPO_ROOT / relative for relative in DOC_FILES]
    selected.extend(REPO_ROOT / relative for relative in DOC_PATHS)

    if mode == "docs-and-code":
        selected.extend(REPO_ROOT / relative for relative in CODE_FILES)
        selected.extend(REPO_ROOT / relative for relative in CODE_PATHS)

    return iter_files(selected)


def build_archive_name(mode: str) -> str:
    timestamp = datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")
    suffix = uuid.uuid4().hex[:8]
    return f"{mode}-{timestamp}-{suffix}.zip"


def write_archive(files: list[Path], mode: str) -> Path:
    EXPORT_ROOT.mkdir(parents=True, exist_ok=True)
    archive_path = EXPORT_ROOT / build_archive_name(mode)

    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "EXPORT_INFO.txt",
            "\n".join(
                [
                    "GridPilot MCP ChatGPT export",
                    f"mode={mode}",
                    f"created_utc={datetime.now(UTC).isoformat()}",
                    f"file_count={len(files)}",
                ]
            )
            + "\n",
        )

        for file_path in files:
            archive.write(file_path, arcname=file_path.relative_to(REPO_ROOT))

    return archive_path


def main() -> int:
    args = parse_args()
    files = build_file_list(args.mode)

    if not files:
        print("No files matched the selected export mode.", file=sys.stderr)
        return 1

    archive_path = write_archive(files, args.mode)
    print(archive_path.relative_to(REPO_ROOT))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
