---
description: Smoke-test the agent-smith CLI binary + docker-compose for api-scan / security-scan / fix-bug. Catches regressions that escape the unit test suite.
---

You are running the agent-smith smoke suite. The goal is a fast, deterministic answer to **"does the binary actually work?"** — every phase merge should end with a green smoke before being declared done.

## How to run

The first argument selects scope. Default is `fast` (no LLM cost).

- `/smoke` or `/smoke fast` — build + tests + CLI binary boots + docker-compose config validates. ~2 minutes, no LLM.
- `/smoke api-scan` — `fast` plus a real api-scan against the configured local target.
- `/smoke security-scan` — `fast` plus a real security-scan against the agent-smith repo.
- `/smoke fix-bug` — `fast` plus a dry-run of fix-bug against a known ticket.
- `/smoke all` — fast + all three deep scans sequentially.

## Steps for `fast` (always run)

Run these in order. Any non-zero exit ends the smoke as **FAIL**.

1. **Build:** `dotnet build` — 0 errors, 0 warnings. Warnings count as failures unless they're the known `SandboxToolHost` obsoletes (5 max).
2. **Tests:** `dotnet test --no-build`. All passing.
3. **CLI binary boot:** `dotnet run --no-build --project src/AgentSmith.Cli -- --help` — exits 0, prints the command list.
4. **CLI pipeline dry-runs:** for each of `api-scan`, `security-scan`, `fix`, `feature`:
   `dotnet run --no-build --project src/AgentSmith.Cli -- <command> --help` — exits 0.
5. **docker-compose config validates:** `docker compose -f deploy/docker-compose.yml config -q` — exits 0. (Skip silently if `deploy/docker-compose.yml` doesn't exist on this branch.)

Report per step: PASS / FAIL with a one-line summary.

## Steps for `api-scan` (in addition to fast)

The operator has a local AuthPort instance at `https://localhost:49190` (target may differ; check `.env` / shell env first). Use the project name `authport-api-security-azure-openai`. Required env: `AZURE_OPENAI_API_KEY`, `AZURE_DEVOPS_TOKEN`.

```
set -a && source .env && set +a && dotnet run --no-build --project src/AgentSmith.Cli -- \
  api-scan \
  --swagger https://localhost:49190/api/swagger/v1/swagger.json \
  --target https://localhost:49190 \
  --project authport-api-security-azure-openai \
  --output summary,console
```

Pass = exits 0 AND emits non-zero `findings` count in the summary AND no `Skipping observation index … invalid` warnings in the log.

## Steps for `security-scan` (in addition to fast)

Run against agent-smith itself.

```
set -a && source .env && set +a && dotnet run --no-build --project src/AgentSmith.Cli -- \
  security-scan in agent-smith-security \
  --output summary,console
```

Pass = exits 0 AND emits a finding count.

## Steps for `fix-bug` (in addition to fast)

Dry-run mode — don't actually post a PR. Operator should provide a `--ticket` ID via env `SMOKE_FIX_TICKET` or it defaults to a stable known ticket on the agent-smith repo.

```
set -a && source .env && set +a && dotnet run --no-build --project src/AgentSmith.Cli -- \
  run "fix-bug in agent-smith for ticket ${SMOKE_FIX_TICKET:-1}" \
  --dry-run \
  --headless
```

Pass = exits 0 AND prints a pipeline plan.

## Output

Final summary block, copy-paste friendly:

```
SMOKE [scope: <fast|api-scan|...>] @ <branch> @ <iso-time>
  build ......................... PASS / FAIL (Xs)
  tests ......................... PASS / FAIL (Xs, N/M)
  cli-boot ...................... PASS / FAIL
  cli-dry-runs .................. PASS / FAIL
  docker-compose ................ PASS / FAIL / SKIP
  api-scan ...................... PASS / FAIL / SKIP
  security-scan ................. PASS / FAIL / SKIP
  fix-bug ....................... PASS / FAIL / SKIP

Overall: PASS / FAIL
```

## Rules

- **Do not invent results.** Run the actual commands. If a step's tooling isn't present (e.g. docker not installed), report SKIP with the reason.
- **Stop on first FAIL only for `fast` scope.** For deep scopes (`api-scan` / `security-scan` / `fix-bug` / `all`), continue running the remaining steps so the operator sees the full picture, but mark the overall as FAIL.
- **Time-box each step** to something sensible. CLI dry-runs should be <10s each. Real scans get a 10-minute budget; abort with FAIL beyond that.
- **No commits, no pushes, no PRs.** Smoke is read-only on git state.
- **Surface real error output** when something fails — not "see logs", give the operator the actual line that explains it.
