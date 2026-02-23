# Agent Smith — Next Phases

## Context for the AI Assistant

You are working on Agent Smith, a self-hosted AI coding agent that reads tickets,
plans code changes, executes them via an agentic loop, and creates pull requests.

The project has completed 21 phases. The current `.context.yaml` is below.
Your job is to design and implement the following phases. Each phase should follow
the existing patterns: a phase plan document, then implementation.

Read the `.context.yaml`, the `coding-principles.md`, and the existing codebase
before starting any phase. All code must follow the quality standards defined there.

### Current .context.yaml

```yaml
meta:
  project: agent-smith
  version: 1.0.0
  type: [agent, pipeline]
  purpose: "Self-hosted AI coding agent: reads issues, plans, codes, creates PRs."

stack:
  runtime: .NET 8
  lang: C#
  infra: [Docker, K8s, Redis]
  testing: [xUnit, Moq, FluentAssertions]
  sdks: [Anthropic, OpenAI, Google-Gemini, Octokit, LibGit2Sharp, YamlDotNet]

arch:
  style: [CleanArch]
  patterns: [Command/Handler, Pipeline, Factory, Strategy, Adapter]
  layers:
    - Domain        # entities, value objects — no deps
    - Contracts     # interfaces, DTOs, config models
    - Application   # handlers, pipeline executor, use cases
    - Infrastructure # AI providers, git, tickets, Redis bus
    - Host          # CLI entry point, DI wiring
    - Dispatcher    # Slack gateway, job spawning, intent routing

behavior:
  pipeline:
    process-ticket:
      trigger: [cli, slack, webhook]
      steps: [FetchTicket, CheckoutSource, LoadCodingPrinciples, AnalyzeCode,
              GeneratePlan, Approve, AgenticExecute, Test, CommitAndPR]
      error: stop-on-first-failure

quality:
  lang: english-only
  limits: { method-lines: 20, class-lines: 120, types-per-file: 1 }
  principles: [SOLID, DRY, GuardClauses, FailFast]
  csharp: [file-scoped-ns, sealed-default, primary-constructors, records-for-DTOs]
  naming:
    classes: PascalCase
    booleans: Is/Has/Can-prefix
    async: Async-suffix
    fields: _camelCase
    interfaces: I-prefix
  testing: { style: AAA, naming: "{Method}_{Scenario}_{Expected}" }

integrations:
  AI:     { type: outbound, providers: [Claude, OpenAI, Gemini] }
  Git:    { type: bidirectional, protocol: SSH/HTTPS, does: "LibGit2Sharp clone/branch/commit/push" }
  GitHub: { type: bidirectional, does: "Issues + PRs via Octokit" }
  GitLab: { type: bidirectional, does: "Issues + MRs via REST" }
  Azure:  { type: bidirectional, does: "Work items + Repos via DevOps SDK" }
  Jira:   { type: inbound, does: "Tickets via REST v3" }
  Slack:  { type: bidirectional, does: "Chat commands + progress via Events API" }
  Redis:  { type: bidirectional, does: "Job queue + progress pub/sub" }

state:
  done:
    p01: "Solution structure, domain entities, contracts, YAML config loader"
    p02: "Command/Handler pattern: 9 context records, 9 handler stubs, CommandExecutor"
    p03: "Providers: AzureDevOps+GitHub tickets, Local+GitHub source, Claude agentic loop"
    p04: "Pipeline execution: IntentParser, PipelineExecutor, ProcessTicketUseCase, DI wiring"
    p05: "CLI (System.CommandLine), Dockerfile, docker-compose, DI integration test"
    p06: "Resilience: Polly retry with exponential backoff + jitter"
    p07: "Prompt caching: CacheConfig, TokenUsageTracker, system prompt optimization"
    p08: "Context compaction: ClaudeContextCompactor, FileReadTracker deduplication"
    p09: "Model registry: per-task model selection, ScoutAgent for codebase discovery"
    p10: "Production container: headless mode, Docker hardening, health checks"
    p11: "Multi-provider: OpenAI GPT-4.1 + Gemini 2.5 agent providers"
    p12: "Cost tracking: RunCostSummary, PricingConfig, per-phase token tracking"
    p13: "Ticket writeback: UpdateStatus, Close, pipeline integration after PR"
    p14: "Webhook trigger: GitHub Action (issues.labeled), WebhookListener (--server)"
    p15: "Azure Repos source provider: clone, branch, PR via DevOps API"
    p16: "Jira ticket provider: REST v3, ADF parsing"
    p17: "GitLab provider: tickets + source (MRs, clone, branch)"
    p18: "Chat gateway: Redis message bus, Dispatcher, Slack adapter, conversation state"
    p19: "K8s manifests, Dispatcher Dockerfile, Kustomize overlays, docker-compose redis"
    p19a: "Docker job spawner: DockerJobSpawner, self-contained docker-compose"
    p20: "Intent engine (regex+Haiku), help command, error UX, agentic Slack updates"
    p21: "Code quality: folder restructuring (6 projects), sealed, constants, boolean naming, 161 tests"
  active: {}
  planned:
    p22: "Prompt optimization, token reduction"
```

---

## Phase 22: CCS Auto-Bootstrap (Language-Agnostic Project Understanding)

### Goal

Agent Smith can work on any repository, not just .NET. When encountering a new
repo without a `.context.yaml`, the agent auto-generates one by detecting the
language, stack, and project structure. This is the foundation for everything else.

### What is CCS?

CCS (Compact Context Specification) is a YAML format that captures project identity,
stack, architecture, and state in ~1,200 tokens instead of loading full documentation
(~50,000 tokens). See the attached `template.context.yaml` and `context.schema.json`
for the format definition.

The `.context.yaml` lives in the repo root. Once generated, it is committed to the
repo and serves as the persistent project context for all future runs.

### Requirements

#### Step 1: Language & Stack Detector (deterministic, zero LLM tokens)

A `ProjectDetector` that scans a repo root and returns a `DetectedProject` record
containing language, runtime, package manager, build commands, test commands,
frameworks, and key project files to read.

Detection rules by marker files:

**.NET:**
- Markers: `*.sln`, `*.csproj`, `global.json`
- Read: all `*.csproj` (for PackageReferences, ProjectReferences, TargetFramework)
- Build: `dotnet build`
- Test: `dotnet test`
- Package manager: NuGet (embedded in csproj)
- Project structure: Solution → Projects → Namespaces

**Python:**
- Markers: `pyproject.toml`, `setup.py`, `setup.cfg`, `requirements.txt`, `Pipfile`
- Read: `pyproject.toml` or `setup.py` or `requirements.txt`
- Build: depends on tooling
- Test detection:
  - `pyproject.toml` contains `[tool.pytest]` → `pytest`
  - `tox.ini` exists → `tox`
  - `Makefile` with test target → `make test`
  - fallback → `pytest`
- Package manager detection:
  - `pyproject.toml` with `[tool.poetry]` → `poetry`
  - `Pipfile` → `pipenv`
  - `uv.lock` → `uv`
  - `requirements.txt` → `pip`
  - `pyproject.toml` with hatchling → `hatch`

**TypeScript / JavaScript:**
- Markers: `package.json`, `tsconfig.json`, `deno.json`
- Read: `package.json` (dependencies, devDependencies, scripts), `tsconfig.json`
- Build detection from `package.json` scripts:
  - `scripts.build` exists → use it
  - Framework config detection: `next.config.*` → Next.js, `angular.json` → Angular,
    `vite.config.*` → Vite, `nuxt.config.*` → Nuxt, `svelte.config.*` → SvelteKit,
    `remix.config.*` → Remix, `astro.config.*` → Astro
- Test detection:
  - `scripts.test` exists → use it
  - `vitest.config.*` → vitest
  - `jest.config.*` → jest
  - `playwright.config.*` → playwright
  - `cypress.config.*` → cypress
- Package manager detection:
  - `pnpm-lock.yaml` → pnpm
  - `yarn.lock` → yarn
  - `bun.lockb` or `bun.lock` → bun
  - `deno.json` or `deno.lock` → deno
  - `package-lock.json` → npm

**General (all languages):**
- CI detection: `.github/workflows/` → GitHub Actions, `azure-pipelines.yml` → Azure DevOps,
  `.gitlab-ci.yml` → GitLab CI, `Jenkinsfile` → Jenkins
- Infra detection: `Dockerfile` → Docker, `docker-compose*.yml` → Docker Compose,
  `k8s/` or `kustomization.yaml` → K8s, `terraform/` or `*.tf` → Terraform
- README.md: read first 300 words for purpose extraction

#### Step 2: Context Generator (one LLM call)

Takes the `DetectedProject` from Step 1 plus the actual content of the key files
and sends it to the LLM with the CCS template. One call, ~3,000 input tokens,
~800 output tokens.

Prompt structure:
```
System: You are a project analyst. Generate a .context.yaml for this repository.
Use ONLY the template format provided. Fill in what you can determine,
leave out sections you cannot determine. Be precise, not verbose.

User: ## Detected Stack
{DetectedProject as YAML}

## Key Files
{content of csproj/package.json/pyproject.toml files}

## README (excerpt)
{first 300 words of README}

## Directory Structure
{tree output, depth 3, excluding node_modules/.git/bin/obj}

## Template
{template.context.yaml}

Generate the .context.yaml. Return ONLY valid YAML, no explanation.
```

#### Step 3: Validation & Commit

- Validate generated YAML against `context.schema.json`
- If invalid: fix and retry once (send validation errors back to LLM)
- Present to user for review (in Slack: formatted message with approve/edit buttons)
- On approval: commit `.context.yaml` to the repo

#### Step 4: Build & Test Command Integration

The `DetectedProject` feeds into the pipeline. Currently the `Test` step is
hardcoded for dotnet. It must use the detected test command:

```csharp
// Before (hardcoded):
await RunCommandAsync("dotnet test", workingDir);

// After (dynamic):
await RunCommandAsync(detectedProject.TestCommand, workingDir);
```

Same for any build verification steps.

### Architecture Notes

- `IProjectDetector` interface in Contracts
- `ProjectDetector` implementation in Infrastructure (one class, switch by markers)
- `DetectedProject` record in Contracts
- `IContextGenerator` interface in Contracts
- `ContextGenerator` implementation in Infrastructure (uses IAgentProvider for the LLM call)
- The detector does NOT use LLM. It's pure file system analysis.
- The generator uses exactly ONE cheap LLM call (Haiku/Flash level).

### Definition of Done

- [ ] Detecting .NET, Python, TypeScript projects correctly
- [ ] Generating valid `.context.yaml` for each
- [ ] Build and test commands correctly detected
- [ ] Pipeline uses detected commands instead of hardcoded ones
- [ ] Schema validation passes
- [ ] Unit tests for detector (mock file system)
- [ ] Integration test: run detector against Agent Smith's own repo

---

## Phase 23: Multi-Repo Support

### Goal

Agent Smith can process a ticket that spans multiple repositories. One ticket →
multiple PRs in different repos, coordinated as a single unit of work.

### Problem

Today's `agentsmith.yml` has a 1:1 relationship: one project config → one repo.
A ticket like "Update the shared API contract and all consuming services" requires
changes in 3 repos: the API repo, Service A, and Service B.

### Requirements

#### Step 1: Config Structure Change

Extend `agentsmith.yml` to support project groups:

```yaml
# Current (stays valid — backward compatible):
projects:
  my-api:
    ticket_provider: github
    source_provider: github
    repo: owner/my-api

# New (optional grouping):
project-groups:
  platform-update:
    description: "Changes that span API + consumers"
    repos:
      - project: my-api
        role: primary          # changes start here
      - project: service-a
        role: consumer         # receives contract changes
      - project: service-b
        role: consumer
    strategy: sequential       # or: parallel, dependency-order
```

- `role: primary` — the repo where the main change happens
- `role: consumer` — repos that need to adapt to the primary change
- `strategy` controls execution order

Individual project definitions remain as they are. Groups reference them.

#### Step 2: Multi-Repo Pipeline Orchestration

New `MultiRepoPipelineExecutor` that:

1. Runs the full pipeline on the `primary` repo first
2. Takes the resulting changes (especially interface/contract changes) as context
3. Runs the pipeline on each `consumer` repo with the additional context:
   "The API contract changed as follows: {diff}. Adapt this service."
4. Creates linked PRs (PR descriptions reference each other)

For `strategy: parallel`, consumer repos run concurrently after primary completes.
For `strategy: dependency-order`, a dependency graph between consumers is respected.

#### Step 3: Cross-Repo Context Passing

The key challenge: How does Service A know what changed in the API?

Approach: After the primary repo's PR is created, extract the diff and include it
in the system prompt for consumer repos:

```
## Cross-Repo Context
The following changes were made in `my-api` as part of the same ticket:
{git diff from primary repo}

Adapt this service to be compatible with these changes.
```

This is straightforward — it's just additional prompt context, no new infrastructure.

#### Step 4: Linked PR Management

- All PRs in a group reference the parent ticket
- PR descriptions include links to related PRs in other repos
- Ticket writeback lists ALL created PRs, not just one
- If any consumer PR fails, the user is notified but other PRs are not rolled back
  (this is a conscious design decision — rollback across repos is a different problem)

### Architecture Notes

- `ProjectGroupConfig` record in Contracts
- `MultiRepoPipelineExecutor` in Application (wraps existing `PipelineExecutor`)
- `CrossRepoContext` record in Domain (carries diff + metadata between runs)
- Existing `ProcessTicketUseCase` gets a branch: single-repo → existing flow,
  multi-repo → new `MultiRepoPipelineExecutor`
- DI wiring: Factory decides based on whether the resolved project is part of a group

### Definition of Done

- [ ] Config parsing for project groups (backward compatible)
- [ ] Sequential multi-repo execution works
- [ ] Cross-repo diff is passed as context
- [ ] Linked PRs reference each other
- [ ] Ticket writeback lists all PRs
- [ ] Single-repo projects still work exactly as before (no regression)
- [ ] Unit tests for group config parsing
- [ ] Integration test with 2 repos

---

## Phase 24: Code Map Generation (Deep Codebase Understanding)

### Goal

Generate a `code-map.context.yaml` that maps the internal structure of a codebase:
interfaces, implementations, key classes, dependencies, DI graph. This gives the
agent deep understanding of WHERE things are and HOW they connect — beyond what
the high-level `.context.yaml` provides.

### Why Not Just Read the Files?

The agent CAN read any file. But without a map, it doesn't know WHICH files to read.
On a 500-file codebase, reading everything costs ~100k tokens. The code map costs
~800 tokens and tells the agent exactly where to look.

### Approach: LLM-Assisted, Not Custom Parsers

We deliberately do NOT build language-specific parsers (Roslyn, TypeScript Compiler
API, etc.). Instead, we feed key project files to the LLM and let it extract the
structure. The cost per generation is ~5,000 tokens — cheaper than building and
maintaining parsers for every language.

The input for each language:

**.NET:**
- All `*.csproj` files (contain ProjectReferences = dependency graph)
- `find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*"` (file listing)
- For each project: all `I*.cs` files in interfaces/contracts folders (interface definitions)
- `Program.cs` or `Startup.cs` (DI registrations)

**Python:**
- Directory listing with `*.py` files
- `__init__.py` files (module exports)
- Files matching `*interface*`, `*abstract*`, `*base*`, `*protocol*` patterns
- `main.py` or `app.py` or `manage.py` (entry points)
- Key class files (files > 50 lines, sorted by size, top 20)

**TypeScript:**
- `package.json` (for monorepo workspace detection)
- Directory listing with `*.ts` / `*.tsx` files
- `index.ts` / `index.tsx` files (barrel exports)
- Files matching `*service*`, `*controller*`, `*repository*`, `*provider*` patterns
- Any DI container config (`container.ts`, `module.ts`, `providers.ts`)
- Route definitions (`routes.ts`, `router.ts`, `app.ts`)

Prompt to LLM:
```
Given the following project files and directory structure, generate a code-map
in YAML format showing:

1. projects/modules and their dependencies on each other
2. key interfaces/contracts and which classes implement them
3. entry points (main, DI setup, route config)
4. architecturally significant classes with a one-line "does" description

Use this format:
{code-map template}

Files:
{detected key files content}

Directory:
{tree output}

Return ONLY valid YAML.
```

### Output Format

```yaml
# code-map.context.yaml — auto-generated, review before commit
generated: 2026-02-22T18:00:00Z
lang: csharp  # or python, typescript

modules:
  AgentSmith.Domain:
    path: src/AgentSmith.Domain/
    deps: []
    key-types:
      - TicketInfo: "Ticket entity with title, body, labels"
      - PlanResult: "Generated plan with steps and target files"

  AgentSmith.Contracts:
    path: src/AgentSmith.Contracts/
    deps: [Domain]
    interfaces:
      ITicketProvider: [FetchAsync, UpdateStatusAsync, CloseAsync]
      ISourceProvider: [CloneAsync, CreateBranchAsync, CommitAndPushAsync, CreatePullRequestAsync]
      IAgentProvider: [GeneratePlanAsync, ExecutePlanAsync]

  AgentSmith.Infrastructure:
    path: src/AgentSmith.Infrastructure/
    deps: [Domain, Contracts]
    implementations:
      ITicketProvider: [AzureDevOpsTicketProvider, GitHubTicketProvider, JiraTicketProvider, GitLabTicketProvider]
      ISourceProvider: [LocalSourceProvider, GitHubSourceProvider, AzureReposSourceProvider, GitLabSourceProvider]
      IAgentProvider: [ClaudeAgentProvider, OpenAiAgentProvider, GeminiAgentProvider]
    factories:
      TicketProviderFactory: "ProjectConfig → ITicketProvider"
      SourceProviderFactory: "ProjectConfig → ISourceProvider"
      AgentProviderFactory: "AgentConfig → IAgentProvider"

entry-points:
  - src/AgentSmith.Host/Program.cs: "CLI + DI wiring"
  - src/AgentSmith.Dispatcher/Program.cs: "Slack gateway + Redis listener"

dependency-graph: "Domain ← Contracts ← Application ← Host, Infrastructure ← Host"
```

### When to Generate

- During bootstrap (Phase 22) — generate alongside `.context.yaml`
- On request ("regenerate code map")
- NOT automatically on every run (it's a snapshot, not live)

### Architecture Notes

- `ICodeMapGenerator` interface in Contracts
- `CodeMapGenerator` implementation in Infrastructure
- Uses the same `IAgentProvider` as the bootstrap — one LLM call
- The code map is committed alongside `.context.yaml`
- Agent reads code map at start of pipeline, uses it to know where to look

### Definition of Done

- [ ] Code map generation for .NET projects
- [ ] Code map generation for Python projects
- [ ] Code map generation for TypeScript projects
- [ ] LLM prompt produces valid, useful output
- [ ] Code map is loaded in pipeline (new LoadCodeMap step or part of AnalyzeCode)
- [ ] Integration test: generate code map for Agent Smith's own repo
- [ ] Integration test: generate code map for a sample Python project
- [ ] Integration test: generate code map for a sample TypeScript project

---

## Phase 25: PR Review Iteration

### Goal

Agent Smith doesn't stop at "PR created". When reviewers leave comments on the PR,
the agent reads the feedback, makes changes, and pushes a new commit. The PR
becomes a conversation, not a one-shot.

### Flow

```
Current:  Ticket → Plan → Execute → Test → Create PR → DONE

New:      Ticket → Plan → Execute → Test → Create PR → Monitor
                                                          ↓
          Reviewer comments on PR ← ← ← ← ← ← ← ← ← ← ↓
                ↓
          Agent reads review comments
                ↓
          Agent plans changes based on feedback
                ↓
          Agent executes changes on same branch
                ↓
          Agent runs tests
                ↓
          Agent pushes new commit
                ↓
          Agent replies to review comments explaining changes
                ↓
          Back to Monitor (until approved or max iterations)
```

### Requirements

#### Step 1: PR Review Listener

Extend existing provider interfaces to read PR review comments:

```csharp
// New method on ISourceProvider:
Task<IReadOnlyList<ReviewComment>> GetPullRequestReviewsAsync(
    string pullRequestId, CancellationToken ct);

// New record:
public sealed record ReviewComment(
    string Id,
    string Author,
    string Body,
    string? FilePath,       // null for general comments
    int? LineNumber,        // null for file-level comments
    DateTimeOffset CreatedAt);
```

Implement for each source provider:
- GitHub: Octokit `PullRequestReview` + `PullRequestReviewComment`
- GitLab: MR notes API
- Azure DevOps: PR threads API

#### Step 2: Review Feedback Pipeline

A new mini-pipeline that runs when review comments are detected:

```
ReviewPipeline: [FetchReviews, AnalyzeReviews, PlanRevisions, Execute, Test, PushCommit, ReplyToReviews]
```

- `FetchReviews` — gets all unaddressed review comments
- `AnalyzeReviews` — classifies: actionable change request vs. question vs. approval vs. nit
- `PlanRevisions` — creates a plan scoped to the review feedback only
- `Execute` — makes changes on the existing branch
- `Test` — runs tests
- `PushCommit` — commits and pushes (not a new PR, just a new commit)
- `ReplyToReviews` — posts replies to each addressed comment

#### Step 3: Trigger Mechanisms

Three ways the review pipeline triggers:

1. **Webhook**: GitHub/GitLab sends `pull_request_review` event → agent runs review pipeline
2. **Slack**: User says "fix PR comments on #58" → agent runs review pipeline
3. **Polling** (fallback): Agent checks for new comments periodically (configurable interval)

For webhooks, extend the existing WebhookListener (Phase 14) to handle PR review events.

#### Step 4: Guardrails

- Max review iterations: configurable, default 3 (prevent infinite loops)
- Only act on comments from authorized reviewers (configurable allowlist, or "any")
- Skip comments that are just approvals or emoji reactions
- If a comment is unclear, ask for clarification (post a reply asking for specifics)
- Never force-push — always add commits

### Architecture Notes

- `ReviewComment` record in Domain
- `IReviewAnalyzer` in Contracts (classifies comments)
- `ReviewPipelineExecutor` in Application (mini-pipeline, reuses existing Execute/Test steps)
- Source provider extensions: `GetPullRequestReviewsAsync`, `ReplyToReviewAsync`
- New webhook event type in WebhookListener
- Config: `review.max_iterations`, `review.authorized_reviewers`, `review.polling_interval`

### Definition of Done

- [ ] Read PR reviews from GitHub, GitLab, Azure DevOps
- [ ] Classify review comments (actionable / question / approval / nit)
- [ ] Execute changes based on review feedback
- [ ] Push new commits to existing PR branch
- [ ] Reply to review comments with explanation of changes
- [ ] Webhook trigger for PR review events
- [ ] Slack trigger ("fix PR comments on #58")
- [ ] Max iteration guardrail
- [ ] Unit tests for review classification
- [ ] Integration test: full review cycle on a GitHub PR

---

## Phase 26: Coding Principles Detection & Adaptation (Revised)

### Goal

When Agent Smith works on a foreign codebase, it detects coding style, architecture
patterns, and development methodology. The LLM interprets raw data — no hundreds
of lines of deterministic detection code needed.

### Key Design Decision

**What code does:** Collect raw data (config files, code samples) — ~60 lines.
**What the LLM does:** Interpret everything (architecture, style, methodology, quality).
Cost: ~1-2 cents per bootstrap (Haiku). Maintenance: near zero.

### Architecture

```
ProjectDetector.Detect(repoPath)                    → DetectedProject (build/test cmds, deterministic)
RepoSnapshotCollector.Collect(repoPath, project)    → RepoSnapshot (raw files, samples, configs)
                    ↓                                        ↓
ContextGenerator.GenerateAsync(project, repoPath, snapshot)
                    ↓
  LLM interprets EVERYTHING: architecture, style, methodology, quality
                    ↓
  .context.yaml with full quality section
```

Also fixes: hardcoded model strings in DI → use existing `ConfigBasedModelRegistry`.

### Implementation Steps

#### Step 1: Replace DetectedSignals with RepoSnapshot

**Delete:** `DetectedSignals.cs`, `ISignalCollector.cs`, `SignalCollector.cs`

**New:** `RepoSnapshot` record (raw data bag, no interpretation):
```csharp
public sealed record RepoSnapshot(
    IReadOnlyList<string> ConfigFileContents,  // raw .editorconfig, .eslintrc, etc.
    IReadOnlyList<string> CodeSamples);        // top 15 source files, first 80 lines
```

**New:** `IRepoSnapshotCollector` + `RepoSnapshotCollector` (~60 lines):
Only two jobs: read config files, collect code samples. No interpretation.

#### Step 2: Update IContextGenerator + ContextGenerator

- `DetectedSignals? signals` → `RepoSnapshot? snapshot`
- `BuildSignalsSection` → `BuildSnapshotSection` — dumps raw config + code samples
- Uses `QualityTemplateExtended` when snapshot present (LLM fills in all fields)

#### Step 3: Fix model registration — use ConfigBasedModelRegistry

Hardcoded `"claude-haiku-4-5-20251001"` in DI → `registry.GetModel(TaskType)`.
Also update `ModelRegistryConfig.ContextGeneration` default MaxTokens 2048 → 3072.

#### Step 4: Update BootstrapProjectHandler + ContextKeys

- `ISignalCollector` → `IRepoSnapshotCollector`
- `ContextKeys.DetectedSignals` → `ContextKeys.RepoSnapshot`

#### Step 5: DI wiring

- `ISignalCollector, SignalCollector` → `IRepoSnapshotCollector, RepoSnapshotCollector`

#### Step 6: Tests

- New: `RepoSnapshotCollectorTests` (config reading, code sample collection)
- Update: `ContextGeneratorTests` (snapshot in BuildUserPrompt)
- Update: `BootstrapProjectHandlerTests` (new constructor + snapshot mock)

### Definition of Done

- [ ] RepoSnapshot collects raw config files and code samples
- [ ] ContextGenerator passes snapshot to LLM for interpretation
- [ ] LLM prompt produces quality section (style, architecture, methodology)
- [ ] Model strings come from ConfigBasedModelRegistry, not hardcoded
- [ ] All existing + new tests pass
- [ ] Works for .NET, Python, TypeScript repos

---

## Phase 27: Structured Command UI (Slack Modals & Teams Adaptive Cards)

### Goal

Replace free-text command input with structured, guided UI. Instead of typing
"fix #58 in agent-smith-test" (error-prone, requires knowing exact syntax),
users get dropdown menus with autocomplete for commands, projects, and tickets.

### Current Problem

Today's flow in Slack:
```
User types: "fix #58 in agent-smith-test"
  → IntentEngine parses with regex, then Haiku fallback
  → Frequently fails: typos, wrong project name, ambiguous input
  → Clarification round-trip wastes time
```

### New Flow

```
User types: /fix  (or /agentsmith, or clicks a shortcut)
  → Dispatcher opens a Modal with dropdowns
  → User selects from validated options — no typos possible
  → Dispatcher receives structured data — no parsing needed
```

### Requirements

#### Step 1: Slash Command → Modal

Register slash commands in the Slack App configuration:
- `/fix` — opens the Fix Ticket modal
- `/agentsmith` — opens a general modal with command selection

When the slash command is received, the Dispatcher responds with `views.open`:

```json
{
  "type": "modal",
  "title": { "type": "plain_text", "text": "Agent Smith" },
  "submit": { "type": "plain_text", "text": "🚀 Go" },
  "blocks": [
    {
      "type": "input",
      "block_id": "command_block",
      "label": { "type": "plain_text", "text": "Command" },
      "element": {
        "type": "static_select",
        "action_id": "command_select",
        "options": [
          { "text": { "type": "plain_text", "text": "🔧 Fix ticket" }, "value": "fix" },
          { "text": { "type": "plain_text", "text": "📋 List tickets" }, "value": "list" },
          { "text": { "type": "plain_text", "text": "➕ Create ticket" }, "value": "create" },
          { "text": { "type": "plain_text", "text": "🔄 Fix PR comments" }, "value": "review" }
        ]
      }
    },
    {
      "type": "input",
      "block_id": "project_block",
      "label": { "type": "plain_text", "text": "Project" },
      "element": {
        "type": "external_select",
        "action_id": "project_select",
        "placeholder": { "type": "plain_text", "text": "Select project..." },
        "min_query_length": 0
      }
    },
    {
      "type": "input",
      "block_id": "ticket_block",
      "label": { "type": "plain_text", "text": "Ticket" },
      "element": {
        "type": "external_select",
        "action_id": "ticket_select",
        "placeholder": { "type": "plain_text", "text": "Search tickets..." },
        "min_query_length": 0
      },
      "optional": true
    }
  ]
}
```

#### Step 2: Dynamic Options Endpoint

The Dispatcher needs a new endpoint that Slack calls when external_select
menus are opened or the user types in the typeahead field.

Slack sends a POST to the configured Options Load URL with the `action_id`
identifying which dropdown needs options.

**Project dropdown** (`action_id: project_select`):
- Load all projects from `agentsmith.yml`
- Return as options list
- No API calls needed — pure config lookup

```csharp
// Pseudo-code for options handler
if (actionId == "project_select")
{
    var projects = config.Projects.Keys
        .Where(name => name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
        .Select(name => new SlackOption(name, name));
    return new { options = projects };
}
```

**Ticket dropdown** (`action_id: ticket_select`):
- Requires knowing which project was selected (Slack sends the current view state)
- Query the project's TicketProvider for open tickets matching the search query
- Return ticket number + title as options
- Cache results for 60 seconds (avoid hammering ticket APIs on every keystroke)

```csharp
if (actionId == "ticket_select")
{
    var projectName = ExtractSelectedProject(payload.View.State);
    var provider = ticketProviderFactory.Create(config.Projects[projectName]);
    var tickets = await provider.SearchAsync(searchQuery, status: "open", ct);
    return new { options = tickets.Select(t =>
        new SlackOption($"#{t.Id} — {t.Title}", t.Id.ToString())) };
}
```

#### Step 3: Modal Submission Handler

When the user clicks "Go", Slack sends a `view_submission` event with all
selected values cleanly structured:

```json
{
  "type": "view_submission",
  "view": {
    "state": {
      "values": {
        "command_block": { "command_select": { "selected_option": { "value": "fix" } } },
        "project_block": { "project_select": { "selected_option": { "value": "agent-smith-test" } } },
        "ticket_block": { "ticket_select": { "selected_option": { "value": "58" } } }
      }
    }
  }
}
```

The Dispatcher extracts the values and routes directly — no IntentEngine needed
for modal submissions. The IntentEngine remains active for free-text messages
(backward compatible).

```csharp
// In Dispatcher:
if (interaction.Type == "view_submission")
{
    var command = ExtractValue(interaction, "command_block", "command_select");
    var project = ExtractValue(interaction, "project_block", "project_select");
    var ticket = ExtractValue(interaction, "ticket_block", "ticket_select");

    // Direct routing — no parsing, no ambiguity
    return command switch
    {
        "fix" => await HandleFixAsync(project, ticket, channelId, ct),
        "list" => await HandleListAsync(project, channelId, ct),
        "create" => await HandleCreateAsync(project, channelId, ct),
        "review" => await HandleReviewAsync(project, ticket, channelId, ct),
        _ => await helpHandler.SendHelpAsync(channelId, ct)
    };
}
```

#### Step 4: Conditional Fields

The modal should be smart about which fields to show:
- "List tickets" → hide ticket dropdown (not needed)
- "Fix ticket" → show ticket dropdown
- "Create ticket" → show title + description text inputs instead
- "Fix PR comments" → show PR dropdown instead of ticket dropdown

Slack supports this via `block_actions` events: when the command dropdown
changes, the Dispatcher updates the modal view with `views.update` to show/hide
relevant fields.

#### Step 5: Teams Adaptive Cards (same concept, different API)

Microsoft Teams uses Adaptive Cards instead of Block Kit. The same modal
concept applies:

```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "Input.ChoiceSet",
      "id": "command",
      "label": "Command",
      "choices": [
        { "title": "🔧 Fix ticket", "value": "fix" },
        { "title": "📋 List tickets", "value": "list" }
      ]
    },
    {
      "type": "Input.ChoiceSet",
      "id": "project",
      "label": "Project",
      "choices": [],
      "style": "filtered",
      "isMultiSelect": false,
      "data": { "type": "Data.Query", "dataset": "projects" }
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "🚀 Go" }
  ]
}
```

Teams dynamic data works via `Data.Query` with a `dataset` property.
The bot responds with filtered results based on user input.

Implement via the existing `IPlatformAdapter` pattern:
- `SlackPlatformAdapter` → builds Block Kit modals
- `TeamsPlatformAdapter` → builds Adaptive Cards
- Both produce structured command data, same downstream handling

### Architecture Notes

- New endpoint on Dispatcher: `POST /slack/options` (Options Load URL)
- New handler: `ModalCommandHandler` in Dispatcher
- `IModalBuilder` interface for platform-agnostic modal construction
- `SlackModalBuilder`, `TeamsModalBuilder` implementations
- Ticket search with caching: `ICachedTicketSearch` (60s TTL, keyed by project+query)
- IntentEngine stays for backward compatibility (free-text still works)
- `/fix` shortcut command → opens modal pre-filled with command="fix"

### Free-Text Fallback

Important: Free-text input continues to work exactly as before. The modal
is an ADDITIONAL input method, not a replacement. Users who prefer typing
"fix #58 in agent-smith-test" can still do that. The modal is for users
who want guided, error-free input.

### Definition of Done

- [ ] Slash command `/fix` opens modal in Slack
- [ ] Slash command `/agentsmith` opens general modal in Slack
- [ ] Project dropdown populated from `agentsmith.yml`
- [ ] Ticket dropdown populated dynamically from ticket provider
- [ ] Ticket search with typeahead filtering
- [ ] Conditional fields based on selected command
- [ ] Modal submission routes correctly without IntentEngine
- [ ] Teams Adaptive Card equivalent
- [ ] Free-text input still works (no regression)
- [ ] Ticket search results cached (60s TTL)
- [ ] Unit tests for modal builder
- [ ] Unit tests for options handler
- [ ] Integration test: full modal flow in Slack

---

## Priority Summary

| Phase | Name | Prio | Depends On |
|-------|------|------|------------|
| 22 | CCS Auto-Bootstrap | 1 | — |
| 23 | Multi-Repo Support | 1 | 22 (uses detected project info) |
| 24 | Code Map Generation | 1 | 22 (runs during bootstrap) |
| 25 | PR Review Iteration | 1 | — (independent) |
| 26 | Coding Principles Detection | 2 | 22 (extends bootstrap) |
| 27 | Structured Command UI | 1 | 18/20 (existing Slack adapter) |

Phases 22, 24, and 26 are closely related (all part of "understand a new repo")
and could be implemented as sub-phases of a single "Project Understanding" epic.

Phase 23 (Multi-Repo) changes the core pipeline orchestration and should be
designed carefully to not break existing single-repo behavior.

Phase 25 (PR Review) is fully independent and can be built in parallel with
anything else.

Phase 27 (Structured Command UI) builds on the existing Slack/Teams infrastructure
from Phase 18/20 and is independent of the bootstrap phases. Can start anytime.

### Implementation Order Recommendation

```
Phase 22 (Bootstrap) ──→ Phase 24 (Code Map) ──→ Phase 26 (Quality Detection)
                    └──→ Phase 23 (Multi-Repo)
Phase 25 (PR Review) ── independent, start anytime
Phase 27 (Command UI) ── independent, start anytime
```
