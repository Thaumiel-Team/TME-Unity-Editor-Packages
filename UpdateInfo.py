import argparse
import datetime as dt
import re
import subprocess
import sys
from pathlib import Path

DEFAULT_VERSION = "1.0.0"
INDENT = "  "
TIMESTAMP_FORMAT = "%Y-%m-%dT%H:%M:%SZ"
LAST_UPDATED_RE = re.compile(r"^(\s+)LastUpdated\s*:\s*.*$")


def run_git(args, cwd):
    result = subprocess.run(
        ["git", *args],
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode != 0:
        print(f"git {' '.join(args)} failed:", file=sys.stderr)
        print(result.stderr, file=sys.stderr)
        sys.exit(1)
    return result.stdout


def top_level_key(line):
    if not line or line[0].isspace() or line.startswith("#"):
        return None
    match = re.match(r"^([^:]+?):\s*(?:#.*)?$", line)
    return match.group(1).strip().strip("'\"") if match else None


def find_blocks(lines):
    starts = []
    for index, line in enumerate(lines):
        key = top_level_key(line)
        if key is not None:
            starts.append((index, key))

    blocks = []
    for position, (start, key) in enumerate(starts):
        end = starts[position + 1][0] if position + 1 < len(starts) else len(lines)
        blocks.append((key, start, end))
    return blocks


def block_indent(lines, start, end):
    for line in lines[start + 1:end]:
        match = re.match(r"^(\s+)\S", line)
        if match:
            return match.group(1)
    return INDENT


def set_last_updated(lines, start, end, timestamp):
    for index in range(start + 1, end):
        match = LAST_UPDATED_RE.match(lines[index])
        if match:
            lines[index] = f"{match.group(1)}LastUpdated: {timestamp}"
            return

    insert_at = end
    while insert_at > start + 1 and not lines[insert_at - 1].strip():
        insert_at -= 1
    lines.insert(insert_at, f"{block_indent(lines, start, end)}LastUpdated: {timestamp}")


def append_entry(lines, key, unity_version, timestamp):
    if lines and lines[-1].strip():
        lines.append("")
    lines.extend([
        f"{key}:",
        f"{INDENT}Version: {DEFAULT_VERSION}",
        f"{INDENT}UnityVersion: {unity_version}",
        f"{INDENT}LastUpdated: {timestamp}",
    ])


def format_timestamp(value):
    return value.astimezone(dt.timezone.utc).strftime(TIMESTAMP_FORMAT)


def last_commit_timestamp(repo, head, directory):
    value = run_git(["log", "-1", "--format=%cI", head, "--", directory], repo).strip()
    if not value:
        return None
    return format_timestamp(dt.datetime.fromisoformat(value))


def changed_package_dirs(repo, base, head):
    output = run_git(["diff", "--name-only", "--no-renames", base, head, "--"], repo)
    directories = set()
    for path in output.splitlines():
        path = path.strip()
        if not path:
            continue
        first = path.split("/", 1)[0]
        if first.startswith(".") or not (repo / first).is_dir():
            continue
        directories.add(first)
    return directories


def write_github_output(path, packages):
    with open(path, "a", encoding="utf-8") as handle:
        handle.write(f"changed={'true' if packages else 'false'}\n")
        handle.write(f"packages={', '.join(packages)}\n")


def main():
    parser = argparse.ArgumentParser(description="Update Info.yml LastUpdated timestamps.")
    parser.add_argument("--base", required=True, help="Commit to compare against")
    parser.add_argument("--head", default="HEAD", help="Commit to compare (default: HEAD)")
    parser.add_argument("--info", default="Info.yml", help="Path to Info.yml (default: Info.yml)")
    parser.add_argument("--output", default=None, help="Path to a GitHub Actions output file")
    args = parser.parse_args()

    repo = Path(__file__).resolve().parent
    info_path = (repo / args.info).resolve()

    lines = info_path.read_text(encoding="utf-8-sig").splitlines() if info_path.exists() else []
    blocks = find_blocks(lines)
    existing_keys = {key for key, _, _ in blocks}

    changed = changed_package_dirs(repo, args.base, args.head)
    now = format_timestamp(dt.datetime.now(dt.timezone.utc))

    for key, start, end in reversed(blocks):
        if key in changed:
            set_last_updated(lines, start, end, now)

    updated = [key for key, _, _ in blocks if key in changed]

    for key in sorted(changed - existing_keys):
        unity_version = last_commit_timestamp(repo, args.head, key) or now
        append_entry(lines, key, unity_version, now)
        updated.append(key)

    if updated:
        info_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"Updated LastUpdated for: {', '.join(updated)}")
    else:
        print("No package directories changed; Info.yml was not modified.")

    if args.output:
        write_github_output(args.output, updated)


if __name__ == "__main__":
    main()
