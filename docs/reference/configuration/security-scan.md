# Security Scan Configuration

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Settings for the security-scan and api-security-scan pipelines: the scan master's budget, the scanner tool files, auto-fix and trend analysis.

## Scan master budget

The scan master (security-master, api-security-master, and the pr-review master) runs on its own limits, separate from the coding master's. They sit on the agent entry the scan uses:

```yaml
agents:
  claude-scan:
    type: claude
    scan_min_source_reads: 6               # read floor; below it the master is driven once more
    scan_master_loop_iterations: 100       # tool iterations per pass
    scan_master_max_output_tokens: 32000   # output budget for the closing findings array
    scan_context_window_tokens: 200000     # input window assumed when the model role states none
```

| Key | Default | What it does |
|-----|---------|--------------|
| `scan_min_source_reads` | 6 | Distinct source files the master must read. Below it, the master is re-prompted once to review every area; the passes' findings are combined. |
| `scan_master_loop_iterations` | 100 | Tool-call iterations per pass. The ceiling the pass ran under is recorded with the run and shown in the scan's account. |
| `scan_master_max_output_tokens` | 32000 | Output budget for the master's final answer. A findings array cut off at this limit is recovered and marked as recovered. |
| `scan_context_window_tokens` | 200000 | Input window the scan assumes, so its conversation can compact instead of running into the provider's limit. A model role that states `context_window_tokens` wins. Set to 0 or less to state nothing. |

What these limits did on a given run shows in the account under **What this scan looked for**; see [Security Scan](../pipelines/security-scan.md#the-scans-account).

## Scanner tool files

Nuclei, Spectral and OWASP ZAP run in the api-security-scan pipeline. Each reads a YAML file, looked up in `$AGENTSMITH_CONFIG_DIR`, then `$AGENTSMITH_CONFIG_DIR/config/`, then `./config/`, the working directory, and the install directory's `config/`. A missing file means the built-in defaults.

`nuclei.yaml`:

```yaml
tags: "api,auth,token,cors,ssl"
exclude_tags: "dos,fuzz"
severity: "critical,high,medium,low"
timeout: 10
retries: 1
concurrency: 10
rate_limit: 50
container_timeout: 180
```

`spectral.yaml` is the Spectral ruleset. The scan fails its Spectral step when none is found.

```yaml
extends:
  - "https://unpkg.com/@stoplight/spectral-owasp-ruleset@2.0.1/dist/ruleset.mjs"
rules: {}
```

`zap.yaml`:

```yaml
container_timeout: 300
```

ZAP picks its own mode: `api-scan` when an OpenAPI description was loaded, `baseline` otherwise. A scanner that reaches its `container_timeout` is reported as cut off, with the limit, and never as a clean result. See [API Scan](../pipelines/api-scan.md#a-cut-off-scanner-says-so).

## Auto-fix

The security-scan pipeline has a `SpawnFix` step that can turn Critical and High findings into fix jobs, each running the `code` pipeline, grouped by file and category, optionally confirmed through [Interactive Dialogue](../concepts/interactive-dialogue.md) first.

The step runs with its defaults: disabled, severity threshold High, confirmation on, at most 3 concurrent jobs. No configuration key turns it on, so every scan logs "Auto-fix disabled, skipping" at this step.

## Trend analysis

Trend analysis needs no configuration. `SecurityTrend` reads the snapshots under `.agentsmith/security/` in the scanned repository and compares the current scan with the most recent one: new findings, resolved findings, and the change in Critical and High counts. `SecuritySnapshotWrite` then writes the current snapshot there as `{date}-{branch}.yaml`. The counts come from the raw scanner findings, so runs compare like with like whatever the master kept.

The snapshot is written into the scan's checkout. For the next scan to compare against it, the file has to reach the repository.

```bash
# View the trend from snapshots in a checked-out repository
agent-smith security-trend --project ./my-api

# Dry run -- show what would be analyzed without executing
agent-smith security-trend --project ./my-api --dry-run
```

## False-positive rules

The masters' filtering rules ship in their skills (`security-master`, `api-security-master`) in the skills catalog; see [Skills Catalog](../../how-it-works/skills-catalog.md) to pin or override them. Project-specific exclusions go in your repository's principles, which `LoadCodingPrinciples` loads and hands to the master. Dismissals recorded in the project's memory reach the master through `LoadMemoryIndex`.

See also: [Security Scan Pipeline](../pipelines/security-scan.md) for the full pipeline documentation.
