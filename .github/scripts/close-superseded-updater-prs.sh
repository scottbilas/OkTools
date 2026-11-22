#!/usr/bin/env bash
#
# Closes open "GitHub Actions Version Updater" PRs that are superseded by a newer, merged updater PR.
#
# A PR is only considered an updater PR when ALL of these hold:
#   - title is exactly "Update GitHub Action Versions"
#   - head branch starts with "gh-actions-update-"
#   - body contains "### GitHub Actions Version Updates"
#   - head branch lives in this repository (not a fork)
#
# Environment:
#   GITHUB_REPOSITORY     owner/repo (required)
#   GITHUB_EVENT_NAME     "pull_request" or "workflow_dispatch" (required)
#   DRY_RUN               "true" to only report what would be closed (default "false")
#
#   pull_request mode (values come from the event payload):
#     MERGED_PR_NUMBER, MERGED_PR_MERGED, MERGED_PR_TITLE, MERGED_PR_HEAD_REF, MERGED_PR_BODY,
#     MERGED_PR_HEAD_REPO
#
#   workflow_dispatch mode:
#     MERGED_PR_NUMBER    optional; a merged updater PR to use as the cutoff. If empty, matching
#                         open updater PRs are only listed and nothing is closed.

set -euo pipefail

readonly UPDATER_TITLE='Update GitHub Action Versions'
readonly UPDATER_BRANCH_PREFIX='gh-actions-update-'
readonly UPDATER_BODY_MARKER='### GitHub Actions Version Updates'

: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"
: "${GITHUB_EVENT_NAME:?GITHUB_EVENT_NAME is required}"
DRY_RUN="${DRY_RUN:-false}"
MERGED_PR_NUMBER="${MERGED_PR_NUMBER:-}"

case "$DRY_RUN" in
    true|false) ;;
    *) echo "::error::DRY_RUN must be 'true' or 'false' (got '$DRY_RUN')"; exit 1 ;;
esac

# shellcheck disable=SC2016 # jq variables, not shell
readonly IS_UPDATER_JQ='
    (.title == $title)
    and ((.headRefName // "") | startswith($prefix))
    and ((.body // "") | contains($marker))
    and (.isCrossRepository == false)'

jq_updater() { # jq with the updater-matching args bound
    jq --arg title "$UPDATER_TITLE" --arg prefix "$UPDATER_BRANCH_PREFIX" --arg marker "$UPDATER_BODY_MARKER" "$@"
}

is_updater_pr() { # $1 = PR json
    jq_updater -e "$IS_UPDATER_JQ" <<<"$1" >/dev/null
}

require_pr_number() { # $1 = value
    if ! [[ "$1" =~ ^[1-9][0-9]*$ ]]; then
        echo "::error::Invalid PR number '$1'"
        exit 1
    fi
}

cutoff=''

case "$GITHUB_EVENT_NAME" in
    pull_request)
        require_pr_number "$MERGED_PR_NUMBER"
        if [[ "${MERGED_PR_MERGED:-}" != 'true' ]]; then
            echo "PR #$MERGED_PR_NUMBER was closed without merging; nothing to do."
            exit 0
        fi
        is_cross_repo=true
        [[ "${MERGED_PR_HEAD_REPO:-}" == "$GITHUB_REPOSITORY" ]] && is_cross_repo=false
        merged_json="$(jq -n \
            --argjson number "$MERGED_PR_NUMBER" \
            --arg title "${MERGED_PR_TITLE:-}" \
            --arg headRefName "${MERGED_PR_HEAD_REF:-}" \
            --arg body "${MERGED_PR_BODY:-}" \
            --argjson isCrossRepository "$is_cross_repo" \
            '{number: $number, title: $title, headRefName: $headRefName, body: $body, isCrossRepository: $isCrossRepository}')"
        if ! is_updater_pr "$merged_json"; then
            echo "Merged PR #$MERGED_PR_NUMBER is not an updater PR; nothing to do."
            exit 0
        fi
        cutoff="$MERGED_PR_NUMBER"
        ;;
    workflow_dispatch)
        if [[ -n "$MERGED_PR_NUMBER" ]]; then
            require_pr_number "$MERGED_PR_NUMBER"
            merged_json="$(gh pr view "$MERGED_PR_NUMBER" --repo "$GITHUB_REPOSITORY" \
                --json number,state,title,headRefName,body,isCrossRepository)"
            if [[ "$(jq -r '.state' <<<"$merged_json")" != 'MERGED' ]] || ! is_updater_pr "$merged_json"; then
                echo "::error::PR #$MERGED_PR_NUMBER is not a merged updater PR; refusing to use it as a cutoff."
                exit 1
            fi
            cutoff="$MERGED_PR_NUMBER"
        fi
        ;;
    *)
        echo "::error::Unsupported event '$GITHUB_EVENT_NAME'"
        exit 1
        ;;
esac

open_json="$(gh pr list --repo "$GITHUB_REPOSITORY" --state open --limit 1000 \
    --json number,title,headRefName,body,url,isCrossRepository)"

matching_json="$(jq_updater -c "[.[] | select($IS_UPDATER_JQ)] | sort_by(.number)" <<<"$open_json")"

if [[ -z "$cutoff" ]]; then
    echo "No merged updater PR given, so nothing will be closed. Open updater PRs:"
    jq -r '.[] | "  #\(.number) \(.url)"' <<<"$matching_json"
    if [[ "$(jq 'length' <<<"$matching_json")" == 0 ]]; then
        echo "  (none)"
    else
        echo "Re-run with 'merged_pr' set to a merged updater PR to close the open ones older than it."
    fi
    exit 0
fi

mapfile -t to_close < <(jq -r --argjson cutoff "$cutoff" '.[] | select(.number < $cutoff) | .number' <<<"$matching_json")

if [[ ${#to_close[@]} -eq 0 ]]; then
    echo "No open updater PRs older than #$cutoff."
    exit 0
fi

comment="Closing as obsolete: superseded by the newer GitHub Actions version updater PR #$cutoff, which has been merged."

failed=0
for number in "${to_close[@]}"; do
    if [[ "$DRY_RUN" == 'true' ]]; then
        echo "[dry run] Would close #$number (superseded by #$cutoff)"
        continue
    fi
    echo "Closing #$number (superseded by #$cutoff)"
    # --comment posts the comment before closing; the branch is intentionally left alone.
    if ! gh pr close "$number" --repo "$GITHUB_REPOSITORY" --comment "$comment"; then
        echo "::warning::Failed to close #$number"
        failed=1
    fi
done

exit "$failed"
