#!/usr/bin/env bash
# Prompt bench — measure a review prompt against a real repository before building
# anything around it.
#
#   tools/prompt-bench.sh <prompt-file> <repo-path> [runs]
#
# Runs the prompt through the agent CLI in the repository, once per run, and writes
# each answer next to the prompt. No pipeline, no pin, no release: the loop that was
# missing while four phases were built on an untested assumption.
#
# Why it exists, and what it is for:
#   - A prompt is a hypothesis. This is the cheapest way to falsify one.
#   - Several runs, because a single run of a review prompt says nothing: three
#     identical scans of one repository have differed by a third in this project.
#   - The answers are kept so two candidates can be read side by side.
set -euo pipefail

PROMPT=${1:?usage: prompt-bench.sh <prompt-file> <repo-path> [runs]}
REPO=${2:?usage: prompt-bench.sh <prompt-file> <repo-path> [runs]}
RUNS=${3:-3}
CLI=${PROMPT_BENCH_CLI:-claude}
MODEL=${PROMPT_BENCH_MODEL:-sonnet}

[ -f "$PROMPT" ] || { echo "no such prompt file: $PROMPT" >&2; exit 1; }
[ -d "$REPO" ]   || { echo "no such repository: $REPO" >&2; exit 1; }
command -v "$CLI" >/dev/null || { echo "agent CLI not found: $CLI" >&2; exit 1; }

OUT="$(cd "$(dirname "$PROMPT")" && pwd)/$(basename "${PROMPT%.*}")-answers"
mkdir -p "$OUT"

echo "prompt : $PROMPT ($(wc -c < "$PROMPT" | tr -d ' ') characters)"
echo "repo   : $REPO"
echo "runs   : $RUNS   model: $MODEL"
echo

for i in $(seq 1 "$RUNS"); do
    started=$(date +%s)
    ( cd "$REPO" && "$CLI" -p --model "$MODEL" < "$PROMPT" ) > "$OUT/run-$i.md" 2>&1 || true
    took=$(( $(date +%s) - started ))
    printf 'run %s: %ss, %s characters -> %s\n' \
        "$i" "$took" "$(wc -c < "$OUT/run-$i.md" | tr -d ' ')" "$OUT/run-$i.md"
done

echo
echo "file:line citations per run (a claim with no place is not a finding):"
for i in $(seq 1 "$RUNS"); do
    n=$(grep -coE '[A-Za-z0-9_./-]+\.(cs|ts|js|py|go|rs|java|rb|php):[0-9]+' "$OUT/run-$i.md" || true)
    printf '  run %s: %s\n' "$i" "$n"
done
