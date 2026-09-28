# API Scan

The **api-security-scan** pipeline scans a running API against its OpenAPI description. Scanner tools run in containers: Nuclei probes the live target, Spectral lints the description, ZAP exercises the running API. The **api-security-master** then triages their output, reading the source too when one is available. Every finding it delivers is checked against the description and put to a refuter, and the run closes with an account of what the scan set out to look for.

!!! info "Pipeline type: structured"
    api-security-scan is a fixed, deterministic step list: no model decides which steps run. A single **api-security-master** agent (the `AgenticMaster` step) carries the analysis over the scanner outputs.

## Pipeline steps

| # | Command | What it does |
|---|---------|-------------|
| 1 | LoadCatalog | Pulls and verifies the skill catalog |
| 2 | PipelineNameInitializer | Stamps the pipeline name for master routing |
| 3 | RatifyScanContract | States what this scan looks for, before any scanner runs |
| 4 | TryCheckoutSource | Resolves the source if one is available; fails only on a `--source-path` that does not exist |
| 5 | SetupRegistryAuth | Pre-stages private-feed credentials (nothing to do without source) |
| 6-7 | BootstrapCheck / BootstrapGate | Checks the source's bootstrap; skipped when there is no source |
| 8 | LoadContext | Loads the target's `.agentsmith/` context files, if present |
| 9 | LoadCodingPrinciples | Loads the target's principles, if present |
| 10 | LoadMemoryIndex | Loads the project's recorded memory, so earlier dismissals are visible to the master |
| 11 | LoadSwagger | Loads and parses the OpenAPI description (file path or URL) |
| 12 | AccountSurfaceDifference | Compares what the API offers with what its declared clients exercise |
| 13 | SessionSetup | Signs in the configured personas; passive mode without them |
| 14 | SpawnNuclei | Runs Nuclei against the target |
| 15 | SpawnSpectral | Lints the description with the OWASP ruleset |
| 16 | SpawnZap | Runs an OWASP ZAP scan against the target |
| 17 | AgenticMaster | Runs the api-security-master over the scanner reports and the description |
| 18 | CollectMasterFindings | Takes the master's triage as the delivered findings |
| 19 | SubstantiateFindings | Checks each finding against the description and puts it to a refuter |
| 20 | DeliverFindings | Writes output in the requested format(s) |
| 21 | AccountScanCoverage | Checks each stated criterion against the steps that really ran |
| 22 | WriteRunResult | Writes `result.md`, including the scan's account |

## What the scan states before it looks

`RatifyScanContract` writes down the scan's criteria before the first scanner runs, one per step the pipeline really has:

- The API surface under test is enumerated from its specification (`LoadSwagger`)
- Known vulnerability templates are executed against the live API (`SpawnNuclei`)
- The API specification is linted for contract defects (`SpawnSpectral`)
- The live API is exercised dynamically (`SpawnZap`)
- Every candidate finding is triaged by the scan master (`AgenticMaster`)
- Every delivered finding is substantiated against the evidence the scan holds (`SubstantiateFindings`)
- The surviving findings are delivered in the requested formats (`DeliverFindings`)

`AccountScanCoverage` checks each one against the execution trail at the end. A criterion whose step never ran, or failed, is outstanding and names why, and the run fails: the command exits with 1. The account is recorded first, so `result.md` and the delivered reports are still written. The account appears under **What this scan looked for** in `result.md`, with the master pass's measurements; see [Security Scan](security-scan.md#the-scans-account).

## Automated scanners

All three run as containers through the tool runner. Docker must be available where Agent Smith runs; images are pulled on first use.

### Nuclei

[Nuclei](https://github.com/projectdiscovery/nuclei) (`projectdiscovery/nuclei:latest`) probes the live target with its template library. Tags, severities, rate limit and the container's time limit come from `nuclei.yaml`; see [Tool configuration](#tool-configuration).

### Spectral

[Spectral](https://github.com/stoplightio/spectral) (`stoplight/spectral:6`) lints the OpenAPI description against the ruleset in `spectral.yaml`, by default the OWASP API security ruleset. It catches design-level issues no runtime scanner can find.

### ZAP

[OWASP ZAP](https://www.zaproxy.org/) (`ghcr.io/zaproxy/zaproxy:stable`) exercises the running target. It runs its `api-scan` mode when a description was loaded and `baseline` otherwise. ZAP's own exit codes 0 to 3 are all valid results; a higher code means ZAP crashed, and the step says so.

### A cut-off scanner says so

Nuclei and ZAP run under a time limit. A scanner that hits it is reported as **cut off**, with the limit: its step line in the run record reads `DEGRADED: cut off at its 180s time limit before it finished`, not "scan completed". Its findings cover only the part of the target it reached.

The scanner summary the master reads ends with a **Dynamic step coverage** section that names each dynamic step and what it contributed:

```
### Dynamic step coverage
- Nuclei: contributed 12 findings.
- ZAP: contributed nothing, and this is not evidence of a clean target — cut off at its 300s time limit before it finished.
```

A step that ran to its end and found nothing says that instead, so an empty result from a finished scan reads differently from an empty result from an interrupted one.

## How the master analyzes

The `AgenticMaster` step loads the **api-security-master** skill. Its prompt carries the scanner reports, the OpenAPI description, the surface difference when one was computed, and the ticket conversation when there is one. Unlike a repository scan, there is no first look without the scanners: an API scan's inputs are those reports.

The master works on the same read-only surface as the security-master (file reads and searches, `http_request` and `web_fetch`, `log_decision`, `recall` / `remember`, no `run_command` and no write tool), under the same scan budget: `scan_master_loop_iterations`, `scan_master_max_output_tokens` and `scan_context_window_tokens`. See [Security Scan](security-scan.md#how-the-master-analyzes).

`CollectMasterFindings` delivers the master's triage only. Raw Nuclei, Spectral and ZAP results are not promoted on their own. When the master's findings array was cut off mid-write, its complete findings are recovered and the run records the recovery. When its answer could not be read as findings at all, the run records the triage as degraded: the triage criterion is not answered, and every output format carries the "TRIAGE DEGRADED" mark.

## Findings are checked against the description

`SubstantiateFindings` resolves each finding's endpoint against the OpenAPI description the scan loaded. A claim about the live target, one that names an endpoint or schema and has no readable source line behind it, is dropped when the endpoint it cites is not in the description: an endpoint the specification never declared is invention. Every other finding is put to a fresh instance with the request and response that produced it and asked to refute it. A refuted finding is downgraded to Medium and carries the reason; it is not deleted.

## What the clients never use

When a repository in the project declares that it consumes the API, the scan compares what the API offers with what that client exercises. The declaration goes on the repo entry and names the API by its description's `info.title`:

```yaml
repos:
  - acme-org/TodoList.Api                             # serves the interface
  - { repo: acme-org/TodoList.Web, consumes: TodoList }   # calls it
```

`AccountSurfaceDifference` reads the declared client's code for call sites and reports three kinds of difference:

- an operation no client calls
- a property an operation accepts that no client sends
- a property an operation returns that no client reads

None of these is raised as a finding. They reach the master as evidence, each paired with the verification-standard requirement that decides whether it matters (function-level access control, mass assignment, excessive data exposure), with the standard's catalogue version stated beside the ids. What the clients exercise is a **lower estimate**: the report says how many client files it read, how many call sites it found and how many files it could not decide, and an entry may be an artefact of an undecided file.

When an input is missing, the difference is **not computed** and says why: no OpenAPI description, no repository declaring that it consumes this API, the client checkout not available to the run, or the client reading produced no usable report. A `consumes:` name that does not match the loaded description's title fails the run, because a difference computed over repositories you did not choose would read as a clean bill. See [agentsmith.yml schema](../configuration/agentsmith-yml-schema.md#per-repo-consumes-declaration).

## Personas

Pass credentials for up to three personas and `SessionSetup` signs them in against the target before the scanners run, so the scan can test authenticated behavior:

```bash
agent-smith api-scan \
  --agent claude-scan \
  --swagger ./swagger.json \
  --target https://api.staging.example.com \
  --admin-user admin --admin-pass "$ADMIN_PASS" \
  --user1-user alice --user1-pass "$ALICE_PASS"
```

The flags are `--admin-user/--admin-pass`, `--user1-user/--user1-pass` and `--user2-user/--user2-pass`. Without any, the scan runs in passive mode.

## Output formats

The `--output` flag accepts comma-separated values:

| Format | Flag | Description |
|--------|------|-------------|
| Console | `console` | Findings printed to stdout (default) |
| Summary | `summary` | Condensed one-line-per-finding output |
| Markdown | `markdown` | `findings.md` written to the output directory |
| SARIF | `sarif` | `findings.sarif` (SARIF 2.1.0) written to the output directory |

```bash
agent-smith api-scan \
  --agent claude-scan \
  --swagger https://api.example.com/swagger.json \
  --target https://api.example.com \
  --output console,sarif,markdown \
  --output-dir ./reports
```

Output directory resolution order, first writable wins:

1. `--output-dir` (if specified)
2. `/output` (Docker container mount)
3. `./agentsmith-output` (local fallback)

## Tool configuration

The scanners read their settings from YAML files looked up in `$AGENTSMITH_CONFIG_DIR`, then `$AGENTSMITH_CONFIG_DIR/config/`, then `./config/`, the working directory, and the install directory's `config/`. `api-scan` sets `AGENTSMITH_CONFIG_DIR` to the directory of the `--config` file, so files placed next to your `agentsmith.yml` are found.

`nuclei.yaml`:

```yaml
tags: "api,auth,token,cors,ssl"
exclude_tags: "dos,fuzz"
severity: "critical,high,medium,low"
timeout: 10
retries: 1
concurrency: 10
rate_limit: 50
container_timeout: 180       # seconds before the container is cut off
```

`spectral.yaml` is a Spectral ruleset:

```yaml
extends:
  - "https://unpkg.com/@stoplight/spectral-owasp-ruleset@2.0.1/dist/ruleset.mjs"
rules: {}
```

`zap.yaml`:

```yaml
container_timeout: 300       # seconds before the container is cut off
```

## Source resolution

With source, the master can anchor a finding to a file and line. Source is optional: without it the scan runs against the description and the live target only, and does not fail. The log says "No source given — passive mode".

Resolution order, first match wins:

1. `--source-path <local-path>` always wins. A path you name must exist: a missing one fails the run rather than quietly scanning without source.
2. With `--project`, the project's first repository: a local repository's path, or a clone of a remote one using the repository's configured credentials.
3. Otherwise, or when the configured path is missing or the clone fails, the scan runs without source.

`--source-url` needs `--source-type`; a URL without a type is refused before the run starts.

The clone uses the repository's default branch. api-scan never checks out ticket branches.

## CLI examples

```bash
# Scan a running API
agent-smith api-scan \
  --agent claude-scan \
  --swagger ./swagger.json \
  --target https://api.staging.example.com

# Scan with a remote swagger URL and the local source
agent-smith api-scan \
  --agent claude-scan \
  --swagger https://api.example.com/swagger/v1/swagger.json \
  --target https://api.example.com \
  --source-path .

# SARIF output for CI integration
agent-smith api-scan \
  --agent claude-scan \
  --swagger ./swagger.json \
  --target https://localhost:5001 \
  --output sarif \
  --output-dir ./test-results

# Dry run — show the pipeline without executing
agent-smith api-scan \
  --agent claude-scan \
  --swagger ./swagger.json \
  --target https://api.example.com \
  --dry-run
```

`--agent` picks an agent from the config's `agents:` catalog and runs the scan without a project. `--project` still works.

!!! warning "Live API required"
    Nuclei and ZAP send requests to a **live, running API**. The `--target` URL must be reachable. Use a staging environment, never production.
