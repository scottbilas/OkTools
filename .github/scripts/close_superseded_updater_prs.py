#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.15"
# dependencies = ["docopt-ng>=0.9.0"]
# ///
"""
Close open "GitHub Actions Version Updater" PRs that are superseded by a newer, merged updater PR.

Usage:
  close_superseded_updater_prs.py [--repo=REPO] [--merged-pr=N] [--dry-run | --no-dry-run]
  close_superseded_updater_prs.py self-test
  close_superseded_updater_prs.py (-h | --help)

Options:
  --repo=REPO     owner/repo to operate on (default: $GITHUB_REPOSITORY)
  --merged-pr=N   Manual mode: merged updater PR to use as the cutoff (default: $MERGED_PR)
  --dry-run       Manual mode: only report what would be closed (default: $DRY_RUN, else true)
  --no-dry-run    Manual mode: actually close the superseded PRs
  -h --help       Show this help

Commands:
  self-test       Run the built-in tests (gh is faked; no network or token needed)

A PR only counts as an updater PR (saadmk11/github-actions-version-updater) when ALL of these hold:
  - title is exactly "Update GitHub Action Versions"
  - head branch starts with "gh-actions-update-"
  - body contains "### GitHub Actions Version Updates"
  - head branch lives in the same repository (not a fork)

Modes:
  - pull_request (from GITHUB_EVENT_NAME/GITHUB_EVENT_PATH): if the closed PR was a merged updater PR, close
    every open updater PR with a lower number targeting the same base branch. Never a dry run.
  - manual (workflow_dispatch or local): with --merged-pr N, N must be a merged updater PR and is used as the
    cutoff for the same base branch; without it, open updater PRs are only listed. Dry run by default.

Requires an authenticated `gh` CLI (GH_TOKEN in Actions).

Examples:
  ./close_superseded_updater_prs.py --repo scottbilas/OkTools --merged-pr 81             # dry run
  ./close_superseded_updater_prs.py --repo scottbilas/OkTools --merged-pr 81 --no-dry-run
  ./close_superseded_updater_prs.py self-test
"""

import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from typing import Any
from unittest import mock

from docopt import docopt

UPDATER_TITLE = "Update GitHub Action Versions"
UPDATER_BRANCH_PREFIX = "gh-actions-update-"
UPDATER_BODY_MARKER = "### GitHub Actions Version Updates"

type PR = dict[str, Any]
"""A pull request in `gh --json` shape."""

PR_FIELDS = "number,state,title,headRefName,baseRefName,body,url,isCrossRepository"


def is_updater_pr(pr: PR) -> bool:
    """`pr` uses `gh --json` field names. Missing/null fields never match."""
    return (
        pr.get("title") == UPDATER_TITLE
        and (pr.get("headRefName") or "").startswith(UPDATER_BRANCH_PREFIX)
        and UPDATER_BODY_MARKER in (pr.get("body") or "")
        and pr.get("isCrossRepository") is False
    )


def pr_from_event(event: dict[str, Any], repo: str) -> PR:
    """Convert a `pull_request` webhook payload into `gh --json` shape."""
    pr = event["pull_request"]
    head = pr.get("head") or {}
    return {
        "number": pr["number"],
        "state": "MERGED" if pr.get("merged") is True else (pr.get("state") or "").upper(),
        "title": pr.get("title"),
        "headRefName": head.get("ref"),
        "baseRefName": (pr.get("base") or {}).get("ref"),
        "body": pr.get("body"),
        "url": pr.get("html_url"),
        "isCrossRepository": (head.get("repo") or {}).get("full_name") != repo,
    }


def superseded(open_prs: list[PR], cutoff: int, base: str) -> list[PR]:
    return sorted(
        (p for p in open_prs if is_updater_pr(p) and p["number"] < cutoff and p.get("baseRefName") == base),
        key=lambda p: p["number"],
    )


def gh(*args: str) -> str:
    # stderr is left attached so gh's own error messages show up in the log.
    return subprocess.run(["gh", *args], check=True, stdout=subprocess.PIPE, text=True).stdout


def gh_json(*args: str) -> Any:
    return json.loads(gh(*args))


def error(message: str) -> int:
    print(f"::error::{message}")
    return 1


def resolve_options(args: dict[str, Any]) -> tuple[str, str | None, bool]:
    """Returns (repo, merged_pr, dry_run); command-line options override the environment."""
    repo = args["--repo"] or os.environ.get("GITHUB_REPOSITORY")
    if not repo:
        sys.exit("error: --repo or GITHUB_REPOSITORY is required")
    merged_pr = args["--merged-pr"] or os.environ.get("MERGED_PR") or None
    if args["--dry-run"]:
        dry_run = True
    elif args["--no-dry-run"]:
        dry_run = False
    else:
        dry_run = os.environ.get("DRY_RUN", "true").lower() != "false"
    return repo, merged_pr, dry_run


def main(argv: list[str] | None = None) -> int:
    args = docopt(__doc__, argv=argv)
    if args["self-test"]:
        return self_test()
    repo, merged_pr, manual_dry_run = resolve_options(args)
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
        dry_run = manual_dry_run
        if merged_pr:
            if not (merged_pr.isascii() and merged_pr.isdigit() and int(merged_pr) > 0):
                return error(f"Invalid PR number '{merged_pr}'")
            merged = gh_json("pr", "view", merged_pr, "--repo", repo, "--json", PR_FIELDS)
            if merged["state"] != "MERGED" or not is_updater_pr(merged):
                return error(f"PR #{merged_pr} is not a merged updater PR; refusing to use it as a cutoff.")
            cutoff = merged["number"]
    else:
        return error(f"Unsupported event '{event_name}'")

    if cutoff is not None and not merged.get("baseRefName"):
        return error(f"Merged PR #{cutoff} has no base branch; refusing to close any PRs.")

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

    to_close = superseded(open_prs, cutoff, merged["baseRefName"])
    if not to_close:
        print(f"No open updater PRs targeting {merged['baseRefName']} older than #{cutoff}.")
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


# --- self-test -----------------------------------------------------------------------------------------------

REPO = "o/r"


def _pr(number: int, state: str = "OPEN", **overrides: Any) -> PR:
    pr: PR = {
        "number": number,
        "state": state,
        "title": UPDATER_TITLE,
        "headRefName": f"{UPDATER_BRANCH_PREFIX}{number}",
        "baseRefName": "main",
        "body": f"{UPDATER_BODY_MARKER}\n* actions/checkout v1 -> v2",
        "url": f"https://github.com/{REPO}/pull/{number}",
        "isCrossRepository": False,
    }
    return pr | overrides


class FakeGh:
    """Stands in for `gh`, serving PRs from a list and recording mutating calls."""

    def __init__(self, prs: list[PR], fail_close: set[int] = frozenset()) -> None:
        self.prs = prs
        self.fail_close = fail_close
        self.closed: list[int] = []

    def __call__(self, *args: str) -> str:
        match args:
            case ("pr", "list", *_):
                assert "baseRefName" in args[args.index("--json") + 1].split(",")
                return json.dumps([p for p in self.prs if p["state"] == "OPEN"])
            case ("pr", "view", number, *_):
                assert "baseRefName" in args[args.index("--json") + 1].split(",")
                found = [p for p in self.prs if str(p["number"]) == number]
                if not found:
                    raise subprocess.CalledProcessError(1, ["gh", *args])
                return json.dumps(found[0])
            case ("pr", "close", number, *rest):
                assert "--comment" in rest and "--delete-branch" not in rest, rest
                if int(number) in self.fail_close:
                    raise subprocess.CalledProcessError(1, ["gh", *args])
                self.closed.append(int(number))
                return ""
            case _:
                raise AssertionError(f"unexpected gh call: {args}")


class SelfTest(unittest.TestCase):
    def setUp(self) -> None:
        self.prs = [
            _pr(70, isCrossRepository=True),  # from a fork
            _pr(71, title="Bump stuff"),
            _pr(72, headRefName="feature/x"),
            _pr(73, body=None),
            _pr(74, body="no marker"),
            _pr(77),
            _pr(80),
            _pr(81, "MERGED"),
            _pr(82, "MERGED", title="Something else"),
            _pr(83, "CLOSED"),
            _pr(90),
        ]
        self.fake = FakeGh(self.prs)
        self.enterContext(mock.patch.dict(os.environ, {"GITHUB_REPOSITORY": REPO}, clear=True))
        self.enterContext(mock.patch.object(sys.modules[__name__], "gh", self.fake))

    def run_main(self, *argv: str, **env: str) -> tuple[int, str]:
        os.environ.update(env)
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = main(list(argv))
        return code, out.getvalue()

    def run_event(self, payload: dict[str, Any]) -> tuple[int, str]:
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete_on_close=False) as f:
            json.dump(payload, f)
            f.close()
            return self.run_main(GITHUB_EVENT_NAME="pull_request", GITHUB_EVENT_PATH=f.name)

    @staticmethod
    def event_for(pr: PR, merged: bool = True, head_repo: str = REPO) -> dict[str, Any]:
        return {
            "pull_request": {
                "number": pr["number"],
                "state": "closed",
                "merged": merged,
                "title": pr["title"],
                "body": pr["body"],
                "html_url": pr["url"],
                "head": {"ref": pr["headRefName"], "repo": {"full_name": head_repo}},
                "base": {"ref": pr["baseRefName"]},
            }
        }

    # matching

    def test_is_updater_pr(self) -> None:
        self.assertTrue(is_updater_pr(_pr(1)))
        for bad in (
            {"title": "Update GitHub Action Versions!"},
            {"title": None},
            {"headRefName": "main"},
            {"headRefName": None},
            {"body": None},
            {"body": ""},
            {"isCrossRepository": True},
            {"isCrossRepository": None},
        ):
            with self.subTest(bad=bad):
                self.assertFalse(is_updater_pr(_pr(1) | bad))

    def test_superseded_only_older_matching(self) -> None:
        open_prs = [p for p in self.prs if p["state"] == "OPEN"]
        self.assertEqual([p["number"] for p in superseded(open_prs, 81, "main")], [77, 80])

    def test_mixed_base_cleanup(self) -> None:
        self.prs.extend([_pr(75, baseRefName="dev"), _pr(76, baseRefName=None)])
        code, _ = self.run_event(self.event_for(_pr(81)))
        self.assertEqual((code, self.fake.closed), (0, [77, 80]))
        self.fake.closed.clear()
        code, out = self.run_main("--merged-pr=81", "--no-dry-run", GITHUB_EVENT_NAME="workflow_dispatch")
        self.assertEqual((code, self.fake.closed), (0, [77, 80]))
        self.assertNotIn("Closing #75", out)

    def test_missing_merged_base_refuses_cleanup(self) -> None:
        code, _ = self.run_event(self.event_for(_pr(81, baseRefName=None)))
        self.assertEqual((code, self.fake.closed), (1, []))
        next(p for p in self.prs if p["number"] == 81)["baseRefName"] = None
        code, _ = self.run_main("--merged-pr=81", "--no-dry-run", GITHUB_EVENT_NAME="workflow_dispatch")
        self.assertEqual((code, self.fake.closed), (1, []))

    def test_pr_from_event(self) -> None:
        self.assertEqual(pr_from_event(self.event_for(_pr(81)), REPO), _pr(81, "MERGED"))
        self.assertTrue(pr_from_event(self.event_for(_pr(81), head_repo="fork/r"), REPO)["isCrossRepository"])
        self.assertEqual(pr_from_event(self.event_for(_pr(81), merged=False), REPO)["state"], "CLOSED")

    # pull_request mode

    def test_merged_updater_closes_older(self) -> None:
        code, out = self.run_event(self.event_for(_pr(81)))
        self.assertEqual((code, self.fake.closed), (0, [77, 80]))
        self.assertNotIn("dry run", out)

    def test_merged_ignores_dry_run_env(self) -> None:
        os.environ["DRY_RUN"] = "true"
        self.run_event(self.event_for(_pr(81)))
        self.assertEqual(self.fake.closed, [77, 80])

    def test_closed_without_merge_does_nothing(self) -> None:
        code, out = self.run_event(self.event_for(_pr(81), merged=False))
        self.assertEqual((code, self.fake.closed), (0, []))
        self.assertIn("without merging", out)

    def test_merged_non_updater_does_nothing(self) -> None:
        for pr in (_pr(81, title="Other"), _pr(81, body=None)):
            with self.subTest(pr=pr):
                code, _ = self.run_event(self.event_for(pr))
                self.assertEqual((code, self.fake.closed), (0, []))
        code, _ = self.run_event(self.event_for(_pr(81), head_repo="fork/r"))
        self.assertEqual((code, self.fake.closed), (0, []))

    def test_close_failure_continues_and_fails(self) -> None:
        self.fake.fail_close = {77}
        code, out = self.run_event(self.event_for(_pr(81)))
        self.assertEqual((code, self.fake.closed), (1, [80]))
        self.assertIn("::warning::Failed to close #77", out)

    # manual mode

    def test_manual_without_cutoff_lists_only(self) -> None:
        code, out = self.run_main("--no-dry-run")
        self.assertEqual((code, self.fake.closed), (0, []))
        self.assertEqual(
            [line.split()[0] for line in out.splitlines() if line.startswith("  #")], ["#77", "#80", "#90"]
        )

    def test_manual_defaults_to_dry_run(self) -> None:
        code, out = self.run_main("--merged-pr=81")
        self.assertEqual((code, self.fake.closed), (0, []))
        self.assertIn("[dry run] Would close #77", out)
        self.assertIn("[dry run] Would close #80", out)

    def test_manual_dry_run_precedence(self) -> None:
        for argv, env, closed in (
            ((), {"DRY_RUN": "false"}, [77, 80]),
            ((), {"DRY_RUN": "true"}, []),
            (("--dry-run",), {"DRY_RUN": "false"}, []),
            (("--no-dry-run",), {"DRY_RUN": "true"}, [77, 80]),
        ):
            with self.subTest(argv=argv, env=env):
                self.fake.closed.clear()
                self.run_main("--merged-pr", "81", *argv, **env)
                self.assertEqual(self.fake.closed, closed)

    def test_manual_merged_pr_from_env(self) -> None:
        code, _ = self.run_main(MERGED_PR="81", DRY_RUN="false")
        self.assertEqual((code, self.fake.closed), (0, [77, 80]))

    def test_manual_rejects_bad_cutoffs(self) -> None:
        for merged_pr in ("0", "-1", "1;rm -rf /", "８１", "82", "83", "90"):
            with self.subTest(merged_pr=merged_pr):
                code, out = self.run_main("--merged-pr", merged_pr, "--no-dry-run")
                self.assertEqual((code, self.fake.closed), (1, []))
                self.assertIn("::error::", out)

    def test_unsupported_event(self) -> None:
        code, _ = self.run_main(GITHUB_EVENT_NAME="push")
        self.assertEqual((code, self.fake.closed), (1, []))

    # command line

    def test_cli_errors(self) -> None:
        for argv in (["--dry-run", "--no-dry-run"], ["--bogus"]):
            with self.subTest(argv=argv), self.assertRaises(SystemExit), contextlib.redirect_stdout(io.StringIO()):
                main(argv)
        del os.environ["GITHUB_REPOSITORY"]
        with self.assertRaises(SystemExit):
            main([])
        self.assertEqual(self.fake.closed, [])


def self_test() -> int:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(SelfTest)
    return 0 if unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful() else 1


if __name__ == "__main__":
    sys.stdout.reconfigure(line_buffering=True)  # keep our lines ordered with gh's stderr in the Actions log
    try:
        sys.exit(main())
    except subprocess.CalledProcessError as e:
        sys.exit(error(f"'{' '.join(e.cmd[:3])} ...' failed with exit code {e.returncode}"))
