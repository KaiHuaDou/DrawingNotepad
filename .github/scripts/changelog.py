import argparse
import re
import sys
from pathlib import Path
from typing import NoReturn

CHANGELOG = Path("CHANGELOG.md")
# 标题形如 ## [v1.2.3]、## 1.2.3 - 2026-10-18；Unreleased 等非版本标题不匹配
HEADING = re.compile(r"^##\s+\[?v?(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)\]?(?:\s+-\s+\S+)?\s*$")
LINK_DEFINITION = re.compile(r"^\[[^\]]+\]:")


def fail(message: str) -> NoReturn:
    print(f"::error::{message}", file=sys.stderr)
    sys.exit(1)


def read_lines() -> list[str]:
    if not CHANGELOG.is_file():
        fail(f"找不到 {CHANGELOG}，请在仓库根目录运行")
    return CHANGELOG.read_text(encoding="utf-8").splitlines()


def read_version() -> str:
    for line in read_lines():
        match = HEADING.match(line)
        if match:
            return match.group(1)
    fail("CHANGELOG.md 中没有找到版本标题（## [vX.Y.Z]）")


def write_notes(tag: str, output: Path) -> None:
    version = tag[1:] if tag.startswith("v") else tag
    lines = read_lines()

    start = None
    for index, line in enumerate(lines):
        match = HEADING.match(line)
        if match and match.group(1) == version:
            start = index
            break
    if start is None:
        fail(f"CHANGELOG.md 中没有 {version} 的小节")

    body: list[str] = []
    for line in lines[start + 1:]:
        if re.match(r"^##\s+", line) or LINK_DEFINITION.match(line):
            break
        body.append(line)

    text = "\n".join(body).strip()
    if not text:
        fail(f"CHANGELOG.md 中 {version} 的小节为空")
    output.write_text(f"{text}\n", encoding="utf-8", newline="\n")


def main() -> None:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)

    version = commands.add_parser("version")
    version.add_argument("--numeric", action="store_true")

    notes = commands.add_parser("notes")
    notes.add_argument("--tag", required=True)
    notes.add_argument("--out", required=True, type=Path)

    args = parser.parse_args()
    if args.command == "version":
        value = read_version()
        print(value.split("-", 1)[0] if args.numeric else value)
        return

    write_notes(args.tag, args.out)


if __name__ == "__main__":
    main()
