#!/usr/bin/env bash
# p0505 — measure what the candidate offline commands actually do against the
# three data-repository fixture shapes.
#
# The deliverable is the table this emits, not a verdict. Every candidate runs
# against every variant of its shape, in a FRESH COPY under a temp directory
# (never inside the fixture tree — the harness re-copies Fixtures/** on every
# build, and the analyzer reads that tree), with no workspace credentials.
#
# Each command is measured twice: once with network, once with `--network none`.
# A row's `network` column is `yes` when the two runs disagreed on the exit code.
#
# Usage:  tools/measure-data-toolchain.sh [output.tsv]
set -euo pipefail

IMAGE_TAG="agentsmith-data-toolchain:p0505"
BASE_IMAGE="python:3.12-bookworm"
DBT_CORE_VERSION="1.11.4"
DBT_DATABRICKS_VERSION="1.12.4"
SQLFLUFF_VERSION="3.4.2"
YAMLLINT_VERSION="1.37.1"
CHECK_JSONSCHEMA_VERSION="0.35.0"
DATABRICKS_CLI_VERSION="1.13.0"

repo_root() {
  local dir="${BASH_SOURCE[0]%/*}"
  dir="$(cd "$dir/.." && pwd)"
  while [ "$dir" != "/" ] && [ ! -f "$dir/AgentSmith.sln" ]; do dir="$(dirname "$dir")"; done
  [ -f "$dir/AgentSmith.sln" ] || { echo "AgentSmith.sln not found above $0" >&2; exit 1; }
  echo "$dir"
}

ROOT="$(repo_root)"
FIXTURES="$ROOT/tests/AgentSmith.PipelineHarness/Fixtures/DataFixture"
OUT="${1:-$ROOT/tests/AgentSmith.PipelineHarness/Reports/data-toolchain/measured-commands.tsv}"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/p0505-measure.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

[ -d "$FIXTURES" ] || { echo "fixtures not found: $FIXTURES" >&2; exit 1; }

# ------------------------------------------------------------------ 1. image
cat > "$WORK/Dockerfile" <<DOCKERFILE
FROM $BASE_IMAGE
RUN pip install --no-cache-dir \\
      "dbt-core==$DBT_CORE_VERSION" \\
      "dbt-databricks==$DBT_DATABRICKS_VERSION" \\
      "sqlfluff==$SQLFLUFF_VERSION" \\
      "yamllint==$YAMLLINT_VERSION" \\
      "check-jsonschema==$CHECK_JSONSCHEMA_VERSION"
RUN set -eux; \\
    arch="\$(dpkg --print-architecture)"; \\
    case "\$arch" in amd64) a=amd64 ;; arm64) a=arm64 ;; *) echo "unsupported \$arch" >&2; exit 1 ;; esac; \\
    curl -fsSL -o /tmp/databricks.zip \\
      "https://github.com/databricks/cli/releases/download/v$DATABRICKS_CLI_VERSION/databricks_cli_${DATABRICKS_CLI_VERSION}_linux_\${a}.zip"; \\
    unzip -q /tmp/databricks.zip -d /usr/local/bin; \\
    rm /tmp/databricks.zip; \\
    chmod +x /usr/local/bin/databricks
DOCKERFILE
echo "building $IMAGE_TAG ..." >&2
docker build -q -t "$IMAGE_TAG" "$WORK" >/dev/null

# --------------------------------------------------- 2. fixtures into a copy
mkdir -p "$WORK/fixtures"
cp -R "$FIXTURES/." "$WORK/fixtures/"

# ------------------------------------------------------- 3. in-container run
cat > "$WORK/run-matrix.sh" <<'RUNNER'
#!/usr/bin/env bash
# Emits: shape \t variant \t command \t exit \t tool_version \t first_line
set -uo pipefail
export HOME=/tmp/home
mkdir -p "$HOME"

DBT_VERSION="dbt-core $(dbt --version 2>/dev/null | sed -n 's/.*installed: \([0-9.]*\).*/\1/p' | head -1)"
DBT_VERSION="$DBT_VERSION + dbt-databricks $(pip show dbt-databricks 2>/dev/null | sed -n 's/^Version: //p')"
SQLFLUFF_V="sqlfluff $(sqlfluff --version 2>/dev/null | sed -n 's/.*version //p')"
YAMLLINT_V="yamllint $(yamllint --version 2>/dev/null | sed -n 's/^yamllint //p')"
CJS_V="check-jsonschema $(check-jsonschema --version 2>/dev/null | sed -n 's/.*version //p')"
DBX_V="databricks $(databricks --version 2>/dev/null | sed -n 's/^Databricks CLI v//p')"

tool_version() {
  case "${1%% *}" in
    dbt) echo "$DBT_VERSION" ;;
    sqlfluff) echo "$SQLFLUFF_V" ;;
    yamllint) echo "$YAMLLINT_V" ;;
    check-jsonschema) echo "$CJS_V" ;;
    databricks) echo "$DBX_V" ;;
    *) echo "unknown" ;;
  esac
}

# The bundle JSON Schema is a PRODUCER artifact, not a gate: emitted once and
# handed to check-jsonschema, the same way profiles.yml is a step-0 input.
SCHEMA=/measure/bundle-schema.json
if [ ! -s "$SCHEMA" ]; then
  (cd /measure/fixtures/bundle/clean && databricks bundle schema) > "$SCHEMA" 2>/dev/null || true
fi

# The failing output's first line — plus the two lines after it, because every
# tool here announces the failure on one line and says WHAT failed on the next
# ("Encountered an error:" / "Compilation Error" / the actual message). Matching
# a single line records the announcement and loses the finding.
FAILURE_MARKER='Encountered an error|Compilation Error|Parsing Error|Runtime Error|Database Error|^Error|^ERROR|error:|FAIL|invalid|unparsable'

first_line() {   # $1 = output file, $2 = exit code
  [ "$2" = "0" ] && { echo "-"; return; }
  local clean; clean="$(mktemp)"
  sed -e 's/\x1b\[[0-9;]*[a-zA-Z]//g' -e 's/\t/ /g' "$1" | grep -v '^[[:space:]]*$' > "$clean"
  local start text
  start="$(grep -n -m1 -E "$FAILURE_MARKER" "$clean" | cut -d: -f1 || true)"
  if [ -n "$start" ]; then
    text="$(sed -n "${start},$((start + 2))p" "$clean" | paste -sd $'\x01' -)"
  else
    text="$(tail -3 "$clean" | paste -sd $'\x01' -)"
  fi
  rm -f "$clean"
  text="${text//$'\x01'/ / }"
  echo "$text" | cut -c1-400
}

measure() {      # $1 = shape, $2 = variant, $3 = command
  local w=/tmp/w out ec
  rm -rf "$w"; cp -R "/measure/fixtures/$1/$2" "$w"
  cp "$SCHEMA" "$w/bundle-schema.json" 2>/dev/null || true
  out="$(mktemp)"
  ( cd "$w" && eval "$3" ) > "$out" 2>&1
  ec=$?
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$ec" "$(tool_version "$3")" "$(first_line "$out" "$ec")"
  rm -f "$out"; rm -rf "$w"
}

DBT_COMMANDS=(
  "dbt deps --profiles-dir ."
  "dbt parse --profiles-dir ."
  "dbt deps --profiles-dir . && dbt parse --profiles-dir ."
  "sqlfluff lint --dialect databricks models"
)
BUNDLE_COMMANDS=(
  "databricks bundle schema"
  "check-jsonschema --schemafile bundle-schema.json databricks.yml resources/sample_job.yml"
  "databricks bundle validate"
)
CONTROL_COMMANDS=( "yamllint ." )

for shape in dbt bundle combined; do
  [ -d "/measure/fixtures/$shape" ] || continue
  commands=()
  case "$shape" in
    dbt)      commands=( "${DBT_COMMANDS[@]}" "${CONTROL_COMMANDS[@]}" ) ;;
    bundle)   commands=( "${BUNDLE_COMMANDS[@]}" "${CONTROL_COMMANDS[@]}" ) ;;
    combined) commands=( "${DBT_COMMANDS[@]}" "${BUNDLE_COMMANDS[@]}" "${CONTROL_COMMANDS[@]}" ) ;;
  esac
  for variant_dir in "/measure/fixtures/$shape"/*; do
    [ -d "$variant_dir" ] || continue
    variant="$(basename "$variant_dir")"
    for cmd in "${commands[@]}"; do measure "$shape" "$variant" "$cmd"; done
  done
done
RUNNER

echo "pass 1/2: with network ..." >&2
docker run --rm -v "$WORK:/measure" "$IMAGE_TAG" bash /measure/run-matrix.sh > "$WORK/rows-net.tsv"
echo "pass 2/2: --network none ..." >&2
docker run --rm --network none -v "$WORK:/measure" "$IMAGE_TAG" bash /measure/run-matrix.sh > "$WORK/rows-nonet.tsv"

# ------------------------------------------------------ 4. merge + hash + emit
mkdir -p "$(dirname "$OUT")"
IMAGE_TAG="$IMAGE_TAG" BASE_IMAGE="$BASE_IMAGE" FIXTURES="$FIXTURES" WORK="$WORK" OUT="$OUT" \
  INSTALL_LINE="pip install dbt-core==$DBT_CORE_VERSION dbt-databricks==$DBT_DATABRICKS_VERSION sqlfluff==$SQLFLUFF_VERSION yamllint==$YAMLLINT_VERSION check-jsonschema==$CHECK_JSONSCHEMA_VERSION + databricks CLI v$DATABRICKS_CLI_VERSION" \
  python3 "${BASH_SOURCE[0]%/*}/measure_data_toolchain_emit.py"

echo "wrote $OUT" >&2
