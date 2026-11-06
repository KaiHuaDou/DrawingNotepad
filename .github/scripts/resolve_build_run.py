import argparse
import json
import os
import subprocess
import sys
import time
from typing import NoReturn


def fail(message: str) -> NoReturn:
    print(f"::error::{message}", file=sys.stderr)
    sys.exit(1)


def capture(*command: str) -> str:
    result = subprocess.run(command, text=True, capture_output=True, check=False)
    if result.returncode != 0:
        fail(result.stderr.strip() or f"{' '.join(command)} 失败")
    return result.stdout.strip()


def watch(run_id: int) -> None:
    result = subprocess.run(["gh", "run", "watch", str(run_id), "--exit-status"], check=False)
    if result.returncode != 0:
        fail(f"构建运行 {run_id} 未成功")


def latest_run(workflow: str, sha: str) -> tuple[int, str] | None:
    runs = json.loads(capture(
        "gh", "run", "list", "--workflow", workflow, "--commit", sha,
        "--limit", "1", "--json", "databaseId,conclusion",
    ) or "[]")
    if not runs:
        return None

    run = runs[0]
    return run["databaseId"], run.get("conclusion") or "pending"


def wait_run(workflow: str, sha: str, timeout: int, interval: int) -> tuple[int, str] | None:
    deadline = time.monotonic() + timeout
    while True:
        run = latest_run(workflow, sha)
        if run is not None or time.monotonic() >= deadline:
            return run
        time.sleep(interval)


def successful_run(workflow: str, sha: str) -> int | None:
    runs = json.loads(capture(
        "gh", "run", "list", "--workflow", workflow, "--commit", sha,
        "--status", "success", "--limit", "1", "--json", "databaseId",
    ) or "[]")
    return runs[0]["databaseId"] if runs else None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workflow", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--wait", type=int, default=60)
    parser.add_argument("--dispatch-wait", type=int, default=600)
    args = parser.parse_args()

    sha = capture("git", "rev-parse", "HEAD")
    run = wait_run(args.workflow, sha, args.wait, 5)

    # 本地打 tag 后立即 push 时构建可能还没建立；只推 tag 或提交只改了文档时则不会有构建
    if run is None:
        print(f"::notice::{sha} 上没有 {args.workflow} 运行，按 {args.tag} 触发构建")
        capture("gh", "workflow", "run", args.workflow, "--ref", args.tag)
        run = wait_run(args.workflow, sha, args.dispatch_wait, 10)

    if run is None:
        fail(f"{args.tag} ({sha}) 上无法建立 {args.workflow} 运行")

    run_id, conclusion = run
    if conclusion == "pending":
        watch(run_id)

    success = successful_run(args.workflow, sha)
    if success is None:
        fail(f"{args.tag} ({sha}) 上没有成功的 {args.workflow} 运行，产物不可信，已中止发版")

    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as stream:
        stream.write(f"run={success}\n")


if __name__ == "__main__":
    main()
