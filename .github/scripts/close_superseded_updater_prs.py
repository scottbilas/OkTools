# /// script
# requires-python = ">=3.12"
# dependencies = ["docopt-ng==0.9.0"]
# ///
"""
Close open "GitHub Actions Version Updater" PRs that are superseded by a newer, merged updater PR.

Usage:
  close_superseded_updater_prs.py [options]

Options:
  --repo=<repo>          owner/repo (default: GITHUB_REPOSITORY)
  --merged-pr=<number>   manual mode cutoff PR (default: MERGED_PR)
  --dry-run              manual mode only; default: DRY_RUN, else true
  --no-dry-run           disable manual-mode dry run
  -h --help              Show this screen.

A PR only counts as an updater PR (saadmk11/github-actions-version-updater) when ALL of these hold:
  - title is exactly "Update GitHub Action Versions"
  - head branch starts with "gh-actions-update-"
  - body contains "### GitHub Actions Version Updates"
  - head branch lives in the same repository (not a fork)

Modes:
  - pull_request (from GITHUB_EVENT_NAME/GITHUB_EVENT_PATH): if the closed PR was a merged updater PR, close
    every open updater PR with a lower number. Never a dry run.
  - manual (workflow_dispatch or local): with --merged-pr N, N must be a merged updater PR and is used as the
    cutoff; without it, open updater PRs are only listed. Dry run by default.

Requires an authenticated `gh` CLI (GH_TOKEN in Actions).

Local usage:
  uv run close_superseded_updater_prs.py --repo scottbilas/NiceIO --merged-pr 118             # dry run
  uv run close_superseded_updater_prs.py --repo scottbilas/NiceIO --merged-pr 118 --no-dry-run
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from typing import Any

from docopt import docopt

UPDATER_TITLE = "Update GitHub Action Versions"
UPDATER_BRANCH_PREFIX = "gh-actions-update-"
UPDATER_BODY_MARKER = "### GitHub Actions Version Updates"

PR_FIELDS = "number,state,title,headRefName,body,url,isCrossRepository"


def is_updater_pr(pr: dict[str, Any]) -> bool:
    """`pr` uses `gh --json` field names. Missing/null fields never match."""
    return (
        pr.get("title") == UPDATER_TITLE
        and (pr.get("headRefName") or "").startswith(UPDATER_BRANCH_PREFIX)
        and UPDATER_BODY_MARKER in (pr.get("body") or "")
        and pr.get("isCrossRepository") is False
    )


def pr_from_event(event: dict[str, Any], repo: str) -> dict[str, Any]:
    """Convert a `pull_request` webhook payload into `gh --json` shape."""
    pr = event["pull_request"]
    head = pr.get("head") or {}
    return {
        "number": pr["number"],
        "state": "MERGED" if pr.get("merged") is True else (pr.get("state") or "").upper(),
        "title": pr.get("title"),
        "headRefName": head.get("ref"),
        "body": pr.get("body"),
        "url": pr.get("html_url"),
        "isCrossRepository": (head.get("repo") or {}).get("full_name") != repo,
    }


def superseded(open_prs: list[dict[str, Any]], cutoff: int) -> list[dict[str, Any]]:
    return sorted((p for p in open_prs if is_updater_pr(p) and p["number"] < cutoff), key=lambda p: p["number"])


def gh(*args: str) -> str:
    # stderr is left attached so gh's own error messages show up in the log.
    return subprocess.run(["gh", *args], check=True, stdout=subprocess.PIPE, text=True).stdout


def gh_json(*args: str) -> Any:
    return json.loads(gh(*args))


def error(message: str) -> int:
    print(f"::error::{message}")
    return 1


def parse_args(argv: list[str] | None) -> dict[str, Any]:
    options = docopt(__doc__, argv=argv)
    repo = options["--repo"]
    if repo is None:
        repo = os.environ.get("GITHUB_REPOSITORY")
    if not repo:
        print("error: --repo or GITHUB_REPOSITORY is required", file=sys.stderr)
        print("Usage: close_superseded_updater_prs.py [options]", file=sys.stderr)
        raise SystemExit(2)

    merged_pr = options["--merged-pr"]
    if merged_pr is None:
        merged_pr = os.environ.get("MERGED_PR") or None

    if options["--dry-run"]:
        dry_run = True
    elif options["--no-dry-run"]:
        dry_run = False
    else:
        dry_run = os.environ.get("DRY_RUN", "true").lower() != "false"

    return {"repo": repo, "merged_pr": merged_pr, "dry_run": dry_run}


def main(argv: list[str] | None = None) -> int:
    sys.stdout.reconfigure(line_buffering=True)  # keep our lines ordered with gh's stderr in the Actions log
    args = parse_args(argv)
    repo: str = args.repo
    event_name = os.environ.get("GITHUB_EVENT_NAME", "")

    if event_name == "pull_request":
        with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as f:
            merged = pr_from_event(json.load(f), repo)
        if merged["state"] != "MERGED":
            print(f"PR #{merged['number']} was closed without merging; nothing to do.")
            return 0
        if not is_updater_pr(merged):
            print(f"Merged PR #{merged['number']} is not an updater PR; nothing to do.")
            return 0
        cutoff: int | None = merged["number"]
        dry_run = False
    elif event_name in ("workflow_dispatch", ""):
        cutoff = None
        dry_run = args.dry_run
        if args.merged_pr:
            if not (args.merged_pr.isascii() and args.merged_pr.isdigit() and int(args.merged_pr) > 0):
                return error(f"Invalid PR number '{args.merged_pr}'")
            merged = gh_json("pr", "view", args.merged_pr, "--repo", repo, "--json", PR_FIELDS)
            if merged["state"] != "MERGED" or not is_updater_pr(merged):
                return error(f"PR #{args.merged_pr} is not a merged updater PR; refusing to use it as a cutoff.")
            cutoff = merged["number"]
    else:
        return error(f"Unsupported event '{event_name}'")

    open_prs = gh_json("pr", "list", "--repo", repo, "--state", "open", "--limit", "1000", "--json", PR_FIELDS)

    if cutoff is None:
        matching = sorted((p for p in open_prs if is_updater_pr(p)), key=lambda p: p["number"])
        print("No merged updater PR given, so nothing will be closed. Open updater PRs:")
        for p in matching:
            print(f"  #{p['number']} {p['url']}")
        if matching:
            print("Re-run with a merged updater PR as the cutoff to close the open ones older than it.")
        else:
            print("  (none)")
        return 0

    to_close = superseded(open_prs, cutoff)
    if not to_close:
        print(f"No open updater PRs older than #{cutoff}.")
        return 0

    comment = f"Closing as obsolete: superseded by the newer GitHub Actions version updater PR #{cutoff}, which has been merged."
    failed = False
    for p in to_close:
        if dry_run:
            print(f"[dry run] Would close #{p['number']} (superseded by #{cutoff})")
            continue
        print(f"Closing #{p['number']} (superseded by #{cutoff})")
        try:
            # --comment posts the comment before closing; the branch is intentionally left alone.
            gh("pr", "close", str(p["number"]), "--repo", repo, "--comment", comment)
        except subprocess.CalledProcessError:
            print(f"::warning::Failed to close #{p['number']}")
            failed = True

    return 1 if failed else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except subprocess.CalledProcessError as e:
        sys.exit(error(f"'{' '.join(e.cmd[:3])} ...' failed with exit code {e.returncode}"))
