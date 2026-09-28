# Security Scan

The **security-scan** pipeline reviews a repository's code for security problems. Deterministic scanners (static patterns, git history, dependency audit) run first. Then the **security-master** reads the source on its own, reconciles what it found with the scanners' output, and produces a curated set of findings. Every delivered finding is then put to a separate refuter, and the run closes with an account of what the scan set out to look for and whether each part was answered. Findings are delivered as SARIF, Markdown, a summary or console output.

!!! info "Pipeline type: structured"
    security-scan is a fixed, deterministic step list: no model decides which steps run. A single **security-master** agent (the `AgenticMaster` step) carries the analysis over the scanner outputs.

## Pipeline steps

| # | Command | What it does |
|---|---------|-------------|
| 1 | LoadCatalog | Pulls and verifies the skill catalog |
| 2 | PipelineNameInitializer | Stamps the pipeline name for master routing |
| 3 | RatifyScanContract | States what this scan looks for, before any scanner runs |
| 4 | CheckoutSource | Clones the repo and checks out the branch to scan: `--branch`, a labelled pull request's head, or the default branch |
| 5 | SetupRegistryAuth | Pre-stages private package-feed credentials for the dependency audit |
| 6-7 | BootstrapCheck / BootstrapGate | Aborts early if the repo was never initialized |
| 8 | LoadContext | Loads the project's `.agentsmith/` context files |
| 9 | LoadCodingPrinciples | Loads the repo's principles |
| 10 | LoadMemoryIndex | Loads the project's recorded memory, so earlier dismissals are visible to the master |
| 11 | StaticPatternScan | Runs the regex patterns from the catalog against the source |
| 12 | GitHistoryScan | Scans the last 500 commits for secrets |
| 13 | DependencyAudit | Runs `npm audit` / `pip-audit` / `dotnet list package --vulnerable` plus structural checks |
| 14 | SecurityTrend | Compares with the previous snapshot |
| 15 | AnalyzeCode | Scout agent maps file structure and dependency graph |
| 16 | AgenticMaster | Runs the security-master: reviews the source, then reconciles the scanner output |
| 17 | MergeMasterFindings | Merges the master's findings with uncovered High+ scanner facts |
| 18 | SubstantiateFindings | Puts every delivered finding to a fresh instance asked to refute it |
| 19 | DeliverFindings | Writes output in the requested format(s) |
| 20 | SecuritySnapshotWrite | Writes the snapshot for the next trend comparison |
| 21 | AccountScanCoverage | Checks each stated criterion against the steps that really ran, and fails the run when the scan did not deliver |
| 22 | WriteRunResult | Writes `result.md`, including the scan's account |

## What the scan states before it looks

`RatifyScanContract` writes down the scan's criteria before the first scanner runs. They are read off the steps this pipeline really has, one per step, so the list cannot promise a step the run does not contain:

- Secrets and unsafe patterns in the working source are identified (`StaticPatternScan`)
- Secrets committed to the repository's history are identified (`GitHistoryScan`)
- Known-vulnerable dependencies are identified (`DependencyAudit`)
- The finding set is compared against the previous snapshot (`SecurityTrend`)
- Every candidate finding is triaged by the scan master (`AgenticMaster`)
- Every delivered finding is substantiated against the evidence the scan holds (`SubstantiateFindings`)
- The surviving findings are delivered in the requested formats (`DeliverFindings`)

At the end, `AccountScanCoverage` checks each criterion against the execution trail. No model is asked. A criterion whose step never ran, or ran and failed, is marked outstanding and says why. That is how a scan whose dependency audit died reads differently from a scan that audited and found nothing. An outstanding criterion fails the run with the missing criterion named: the command exits with 1. The account is recorded first, so `result.md` still lists every criterion, and the findings already delivered (SARIF, Markdown) stay on disk.

## Static pattern scan

The `StaticPatternScan` step runs regex patterns across the source. Patterns ship in the [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills) release tarball alongside skills, and are loaded from `{cacheDir}/patterns/*.yaml` after the catalog is pulled. The catalog ships these categories:

| Category | Patterns | Examples |
|----------|----------|----------|
| **secrets** | 27 | AWS keys, GitHub tokens, private keys, connection strings |
| **injection** | 16 | SQL injection, command injection, XPath, template injection |
| **config** | 15 | Debug mode enabled, permissive CORS, missing security headers |
| **ssrf** | 12 | URL construction from user input, DNS rebinding vectors |
| **ai-security** | 11 | Prompt injection, `eval()` on model output, model output in SQL |
| **compliance** | 10 | PII logging, missing encryption, weak hashing algorithms |
| **api-auth** | 8 | JWT validation switched off, hardcoded signing keys, `AllowAnonymous` on state-changing endpoints |
| **auth** | 4 | Load-by-id without an ownership predicate (IDOR candidates) |

Pattern files are extensible. Contribute upstream via a PR against [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills), or override per deployment via `AGENTSMITH_CONFIG_DIR`. See [Custom Security Patterns](../security/custom-patterns.md) for both paths.

Files that declare themselves generated (an `<auto-generated>` header) are skipped for non-secret patterns: injection or config findings in code nobody hand-edits are noise. Secret detection still runs in generated files, because a committed credential is live regardless of who wrote the file.

## Git history scan

The `GitHistoryScan` step scans the last 500 commits with the `secrets` patterns, for secrets that were committed and later removed. A secret found **only in git history** is reported as **Critical**: it stays exposed even though the current codebase looks clean, and rotated-away-but-still-in-history is exactly what attackers mine. A secret still present in the working tree is reported as **High**.

When a pattern names a provider (AWS, GitHub, Stripe and so on), the finding carries that provider and a **revoke URL**, so the credential can be rotated straight away.

## Dependency audit

The `DependencyAudit` step runs the audit tool for the ecosystem it detects, inside the sandbox:

- **npm audit** for Node.js projects
- **pip-audit** for Python projects (installed on demand when missing)
- **dotnet list package --vulnerable** for .NET projects
- **Structural checks**: missing lockfiles, wildcard version ranges, deprecated packages

A repository with no supported package manager is skipped, and the step says so.

## How the master analyzes

The `AgenticMaster` step loads the **security-master** skill from the catalog and runs the review.

**It looks first, without the scanners' list.** The first turn carries the source, the ticket conversation when there is one, and the project context, but not the scanner output. A list of suspects is an anchor: given one, the cheapest correct-looking behavior is to work the list. So the master inventories the surface itself and reports what it finds.

**Then it reconciles.** In a second turn on the same conversation, the scanners' raw output is put in front of it. For each scanner fact it says whether it already covered it, now judges it real and adds it, or dismisses it, naming the code that makes it not exploitable. It then returns its complete finding list. When the scanners found nothing, there is no second turn. If the reconciliation turn fails, the first turn's findings stand.

**A shallow review is driven once more.** Coverage is counted as the number of distinct source files the master actually read. Below `scan_min_source_reads` (default 6), the master is re-prompted once to inventory the full surface and review every area. The delivered answer is the union of every pass, so a finding from the first pass survives even when the deeper pass does not repeat it. The run log breaks the delivered set down by the pass that first produced each finding.

**It reads, it does not change.** The master's tools are read-only on the source: `read_file`, `grep_in_file`, `grep_in_tree`, `find_files`, `list_directory` and `directory_tree`, plus `http_request` and `web_fetch` to reach a target or a vendor advisory, `log_decision` to record why it dropped something, and `recall` / `remember` for the project's memory (`remember` only proposes an entry under `.agentsmith/memory/`). There is no `run_command` and no write tool. With sub-agents enabled it can delegate parts of the review to children on the same read-only surface. A finding that claims source analysis of a file the master never read is downgraded to a "potential" finding.

**It runs on its own budget.** The scan master's limits are separate from the coding master's, set on the agent entry:

```yaml
agents:
  claude-scan:
    type: claude
    scan_min_source_reads: 6               # read floor before the one re-drive
    scan_master_loop_iterations: 100       # tool iterations per pass
    scan_master_max_output_tokens: 32000   # budget for the closing findings array
    scan_context_window_tokens: 200000     # input window assumed when the model role states none
```

The input window lets the scan compact its conversation instead of running into the provider's context limit; a model role that states its own `context_window_tokens` wins over it. The ceiling the pass ran under is recorded with the run.

**A cut-off answer is recovered, and says so.** When the master's findings array is cut off mid-write, the complete findings in it are recovered and delivered. The run records that the triage was recovered from a truncated answer, and the account's triage line carries that note.

The master's methodology ships as the `security-master` skill in the [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills) catalog; see [Skills Catalog](../../how-it-works/skills-catalog.md) for how to pin or override it.

## Delivery: curated triage with a safety net

The `MergeMasterFindings` step decides what goes forward:

1. The master's findings are the primary delivered set.
2. Every **High or Critical scanner fact** the master's findings don't cover is promoted alongside them. Low and medium scanner noise the master doesn't restate is dropped.
3. Collision identity is *(file, start line)*: when a master finding and a scanner fact point at the same location, the master's version wins.
4. A High+ static-pattern fact in a file the master **read** and chose not to flag counts as dismissed and is not promoted. If the master never read the file, the fact is promoted. Git-history secrets and dependency CVEs are always promoted, because reading the current source cannot refute a secret in history or a vulnerable package version.

## Every delivered finding faces a refuter

`SubstantiateFindings` puts every delivered finding, whoever raised it, to a fresh model instance together with the source it cites, and asks it to refute the claim. A finding it refutes with a quote from the evidence is **downgraded, never deleted**: it drops to Medium, is marked `refuted`, and carries the refuter's reason. The reviewer decides; the scan does not hide the disagreement.

A finding whose citation cannot be resolved is kept when someone authored it: a master's finding whose cited file could not be opened is delivered as written. Only a promoted scanner fact that nobody vouched for, whose citation points at nothing the scan holds, is dropped as invention. If the refuter returns no usable answer, every finding stands. The step's result line says how many findings it delivered, how many are critical and how many were refuted.

## When the scan could not triage

If the master produced no answer, or an answer that is not a findings list, the scan cannot claim a triage. The delivered set is then the raw scanner output, and the run says so everywhere:

- The account marks the triage criterion as not answered, with the reason, and the run is recorded as failed.
- Every output format carries the mark: "TRIAGE DEGRADED — these findings were NOT triaged by the scan master", as a blockquote in Markdown, a banner in console and summary output, and a failed invocation notification in SARIF.

An untriaged scan delivers more findings than a triaged one, so without the mark it would read as the more thorough result.

## The scan's account

`WriteRunResult` renders the account under **What this scan looked for** in `result.md`, and in the ticket comment when a ticket started the scan. Each criterion shows as satisfied or not, with its note. Below the criteria, two measured lines describe the master's pass:

- the system prompt's size and the sizes of the review prompt's parts (conversation, scanner findings, OpenAPI document, surface difference), in characters
- the turns the pass used against its ceiling, and how many distinct source files it read

A run with no master pass omits the measured lines rather than printing zeroes.

## Output formats

The `--output` flag takes a comma-separated list of `console` (default), `summary`, `markdown` and `sarif`. Markdown is written to `findings.md` and SARIF to `findings.sarif` in the output directory.

=== "Console (default)"

    Findings printed to stdout with severity coloring:

    ```
    [CRITICAL] AWS Access Key in git history — config/aws.json (commit a1b2c3d, 2025-11-03)
              Provider: AWS | Revoke: https://console.aws.amazon.com/iam/home#/security_credentials
    [HIGH] SQL Injection in UserRepository.cs:47
           String concatenation in WHERE clause with user-supplied email parameter
    [MEDIUM] Missing HttpOnly flag on auth cookie — AuthController.cs:23
    ```

=== "SARIF"

    SARIF 2.1.0. Import into GitHub Advanced Security, Azure DevOps, or any SARIF viewer:

    ```json
    {
      "$schema": "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/main/sarif-2.1/schema/sarif-schema-2.1.0.json",
      "version": "2.1.0",
      "runs": [{
        "tool": { "driver": { "name": "Agent Smith Security" } },
        "results": [
          {
            "ruleId": "AS001",
            "level": "error",
            "message": { "text": "SQL Injection in UserRepository.cs:47" },
            "locations": [{ "physicalLocation": { "artifactLocation": { "uri": "src/Repositories/UserRepository.cs" }, "region": { "startLine": 47 } } }]
          }
        ]
      }]
    }
    ```

=== "Markdown"

    A report written to `findings.md`:

    ```markdown
    ## Agent Smith Security Review

    Found **14** issues (1 critical, 4 high, 6 medium, 3 low, 0 info)

    ### CRITICAL: AWS Access Key in Git History

    **Location:** `config/aws.json`
    ...
    ```

## CLI examples

```bash
# Scan a local checkout, console output
agent-smith security-scan --agent claude-scan --source-path .

# SARIF output for CI integration
agent-smith security-scan --agent claude-scan --source-path . --output sarif --output-dir ./reports

# Scan a specific branch, markdown output
agent-smith security-scan --agent claude-scan --source-path ./my-api --branch feature/auth --output markdown

# Dry run — show the pipeline without executing
agent-smith security-scan --agent claude-scan --source-path ./my-project --dry-run

# Combine output formats
agent-smith security-scan --agent claude-scan --source-path ./my-project --output sarif,markdown,console --output-dir ./reports
```

`--branch` checks out that branch and scans it in full; there is no diff-only scan. `--agent` picks an agent from the config's `agents:` catalog and runs the scan without a project. `--project` still works and scans the project's repositories; in a multi-repo project, `--repo NAME` scopes the scan to one of them.

!!! tip "CI/CD integration"
    Use `--output sarif` in your CI pipeline and upload the result to GitHub Advanced Security or Azure DevOps. The command exits with 1 when the run fails, including a scan that did not deliver what it stated it would look for, and 0 otherwise; findings alone do not change the exit code. See [GitHub Actions](../cicd/github-actions.md), [Azure DevOps](../cicd/azure-devops.md), and [GitLab CI](../cicd/gitlab-ci.md) for ready-to-use pipeline configurations.

## Exclusion rules

What the master filters out comes from two places. Its general rules (test-only code paths, placeholder credentials, DoS without an exploit path, path-only SSRF, races without reproducible evidence) ship in the `security-master` skill. Your repository's principles, each context's `principles.md` or the file a pipeline's `coding_principles_path` names, are loaded by `LoadCodingPrinciples` and put in front of the master, so project-specific exclusions belong there. Dismissals recorded in the project's memory reach the master through `LoadMemoryIndex`.

## Trend analysis

`SecurityTrend` reads the snapshots under `.agentsmith/security/` in the scanned repository and compares the current finding counts with the most recent one. `SecuritySnapshotWrite` writes the current snapshot there as `{date}-{branch}.yaml`, counted from the raw scanner findings so the trend compares like with like across runs.

Use `agent-smith security-trend --project <dir>` to view the trend from the CLI. See [Security Scan Configuration](../configuration/security-scan.md#trend-analysis).
