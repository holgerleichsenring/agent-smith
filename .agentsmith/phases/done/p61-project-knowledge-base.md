# Phase 61: Project Knowledge Base — Karpathy Pattern for Codebases

> Inspired by Andrej Karpathy's "LLM Knowledge Bases" (April 2, 2026)
> Core insight: "Every business has a raw/ directory. Nobody's ever compiled it."

---

## Goal

Agent Smith applies the Karpathy pattern to codebases.

Karpathy: `raw/ → LLM compiles → wiki/ → Q&A → output → back into wiki`

Agent Smith today: `repo/ → BootstrapProject → .agentsmith/ → pipeline → runs/`

The gap: `.agentsmith/` grows with every run, but nobody can query it.
No query mode, no linting, no accumulating intelligence over time.

Phase 61 closes this gap with three extensions:

| Part | What | Karpathy equivalent |
|---|---|---|
| **p61a** CompileKnowledge | `.agentsmith/` as a living wiki | Compilation step |
| **p61b** QueryIntent | Questions against the wiki | Q&A mode |
| **p61c** WikiLint | Consistency + new questions | Linting / health checks |

---

## p61a: CompileKnowledge — The Living Codebase Wiki

### The Problem Today

`.agentsmith/` after 20 runs:
```
.agentsmith/
  context.yaml          (generated once, then stale)
  code-map.yaml         (generated once, then stale)
  coding-principles.md  (generated once, rarely updated)
  decisions.md          (grows, but unstructured)
  runs/
    r01/ r02/ ... r20/  (gold mine — but inaccessible)
  security/
    2026-04-02-main.sarif
```

A new agent working on ticket #21 reads `context.yaml` and `code-map.yaml` —
but not the 20 previous runs, not `decisions.md`, not the security history.
That's lost institutional knowledge.

### The Solution: wiki/ as Compiled Knowledge

```
.agentsmith/
  wiki/                 ← NEW: LLM-compiled, linked knowledge
    index.md            (master index of all articles)
    architecture.md     (how the system is built and why)
    patterns.md         (detected code patterns and conventions)
    decisions.md        (compiled from all runs/ — why, not what)
    known-issues.md     (recurring problems and their solutions)
    security.md         (security trend summary)
    concepts/           (domain-specific concepts, auto-generated)
      PaymentService.md
      AuthFlow.md
      ...
  context.yaml          (remains, informed by wiki/)
  code-map.yaml         (remains, informed by wiki/)
  runs/                 (source data, referenced directly by path)
  security/             (SARIF snapshots, referenced directly by path)
```

No `raw/` directory, no symlinks. `CompileKnowledgeHandler` references
`runs/` and `security/` directly by path.

### CompileKnowledgeCommand

New pipeline command. Runs:
1. After `WriteRunResult` (incremental — only add new content), subject to rate limiting
2. Explicitly via `agent-smith compile-wiki` CLI
3. Periodically as background job

```
agent-smith compile-wiki --project my-api
agent-smith compile-wiki --project my-api --full   # full recompilation
```

Rate limiting config:

```yaml
knowledge_base:
  compile_interval_minutes: 60   # default: max 1x per hour
  compile_on_every_run: false    # default: false (only when interval expired)
  compile_model: haiku           # default: Haiku (cost-effective)
```

With `compile_on_every_run: true`, it runs after every run.
For high-frequency teams → `compile_interval_minutes: 240`.

**Incremental mode (default):**

```csharp
public sealed class CompileKnowledgeHandler(
    IAgentProvider agentProvider,
    ISourceProvider sourceProvider,
    ILogger<CompileKnowledgeHandler> logger)
    : ICommandHandler<CompileKnowledgeContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CompileKnowledgeContext context, CancellationToken ct)
    {
        var wikiDir = Path.Combine(context.Repository.LocalPath,
            ".agentsmith", "wiki");
        Directory.CreateDirectory(wikiDir);

        // What's new since last compilation?
        var runsPaths = Directory.GetDirectories(
            Path.Combine(context.Repository.LocalPath, ".agentsmith", "runs"));
        var securityPaths = Directory.GetFiles(
            Path.Combine(context.Repository.LocalPath, ".agentsmith", "security"),
            "*.sarif");

        var newRuns = FindRunsSinceLastCompile(runsPaths, context);
        if (!newRuns.Any())
            return CommandResult.Ok("Wiki up to date — nothing new");

        // Incremental LLM call: only incorporate new runs
        var prompt = BuildIncrementalPrompt(newRuns, wikiDir);
        var result = await agentProvider.CompileKnowledgeAsync(prompt, ct);

        // LLM writes/updates wiki files
        foreach (var (file, content) in result.WikiUpdates)
        {
            var path = Path.Combine(wikiDir, file);
            await File.WriteAllTextAsync(path, content, ct);
        }

        // Commit wiki changes to default branch (not PR branch)
        await sourceProvider.CommitToDefaultBranchAsync(
            context.Repository,
            ".agentsmith/wiki/",
            $"chore: update project wiki ({newRuns.Count} new runs)",
            ct);

        return CommandResult.Ok(
            $"Wiki updated: {result.WikiUpdates.Count} files, " +
            $"{newRuns.Count} new runs incorporated");
    }
}
```

Wiki commits go to the **default branch**, not the PR branch.
Rationale: the wiki is project-wide accumulated knowledge,
not PR-specific output. This prevents wiki conflicts on merges.

### LLM Prompt for Compilation

```
You are a knowledge compiler for a codebase.
You receive new run outputs and update an existing wiki.

## Existing Wiki (summary)
{index.md content}

## New Runs Since Last Compilation
{for each run: plan.md + result.md + decisions}

## Tasks
1. Update `decisions.md` — incorporate new decisions, identify patterns
2. Update `known-issues.md` — document recurring problems
3. Update `patterns.md` — new code patterns the agent used
4. Create new concept articles when new concepts appeared
5. Update `index.md` with new articles and backlinks
6. Mark outdated information in existing articles

Write precisely, close to code, from the perspective of a senior engineer.
No marketing speak. Focus on: Why was it done this way?
What didn't work? What are the pitfalls?

Respond as JSON: { "wiki_updates": { "filename": "content" } }
```

### Wiki Article Example: `decisions.md`

```markdown
# Decision Wiki — my-api

_Compiled by Agent Smith from 23 runs. Last updated: 2026-04-02_

## Architecture

### Repository Pattern over direct EF Core
**Decided in:** r03, r07, r12
**Why:** Three independent runs chose the same pattern.
r03: "better testability through mockability"
r07: "consistency with existing services"
r12: "transaction handling across multiple entities simpler"
**Warning:** r15 attempted to use EF directly → test issues →
rolled back. Do not retry.

### Redis for Session State
**Decided in:** r09
**Why:** "InMemory cache doesn't survive deployment restart,
Redis persists and is shareable across pods"
**Pitfall:** Serialization of `UserContext` had circular reference issue
(r09, r11). Fix: explicit DTOs for Redis, no domain objects directly.

## Known Issues

### PaymentService Timeout with >50 Items
**First seen:** r14 | **Again:** r18, r21
**Cause:** N+1 query in `LoadItemPrices()` — agent "discovered" this
three times. Fixed in r14 and r18, reintroduced in r21.
**Permanent fix:** Eager loading in `LoadItemPricesAsync` — NOT lazy.
If you work here: read r14 result.md.
```

---

## p61b: QueryIntent — Questions Against the Wiki

### New Intent Type

Alongside `FixTicketIntent`, `SecurityScanIntent` etc., `QueryIntent` is added:

```
@agent-smith why do we use Repository Pattern in my-api?
@agent-smith what are the most common errors in PaymentService?
@agent-smith show me all auth decisions from the last 3 months
@agent-smith what didn't work in run r14?
```

The `@agent-smith` prefix is **mandatory** for query intents —
this prevents collisions with ticket descriptions that happen to start
with question words.

**No container job.** No checkout. No commit.
The dispatcher reads `.agentsmith/wiki/` directly and answers in chat.
Duration: 5–15 seconds instead of minutes.

### QueryHandler

```csharp
public sealed class QueryHandler(
    ISourceProvider sourceProvider,
    IAgentProvider agentProvider,
    IPlatformAdapter adapter,
    ILogger<QueryHandler> logger)
{
    public async Task HandleAsync(QueryIntent intent, CancellationToken ct)
    {
        // Sparse checkout: only .agentsmith/wiki/
        var wikiPath = await sourceProvider.CheckoutPartialAsync(
            intent.ProjectName,
            includePaths: [".agentsmith/wiki/"],
            ct);

        var wikiDir = Path.Combine(wikiPath, "wiki");
        if (!Directory.Exists(wikiDir))
        {
            await adapter.SendMessageAsync(intent.ChannelId,
                "ℹ️ No wiki for this project yet. " +
                "Run `agent-smith compile-wiki` to create one.", ct);
            return;
        }

        // Read wiki index for navigation
        var index = await File.ReadAllTextAsync(
            Path.Combine(wikiDir, "index.md"), ct);

        // LLM navigates the wiki and answers the question
        var answer = await agentProvider.QueryWikiAsync(
            intent.Question, wikiDir, index, ct);

        // Answer in chat + optional link to source file
        await adapter.SendMessageAsync(intent.ChannelId,
            FormatAnswer(answer), ct);
    }
}
```

The dispatcher uses Git sparse checkout to read only `.agentsmith/wiki/`
(~2-5 seconds instead of 15-30 for full clone). No container job —
runs directly in the dispatcher process.

### LLM Prompt for Query

```
You are a knowledge assistant for the codebase "{project}".
You have access to a wiki at .agentsmith/wiki/.

## Wiki Index
{index.md}

## Question
{intent.Question}

## Procedure
1. Determine which wiki files are relevant
2. Read those files (you have the read_file tool)
3. Answer the question precisely with source references
4. Point out relevant related articles

Respond in format:
**Answer:** [direct answer]
**Source:** [wiki/filename.md, runs/r14/result.md]
**Related articles:** [if relevant]
```

### Answer in Slack

```
🧠 Agent Smith — Knowledge from my-api

**Why Repository Pattern?**

Three independent runs (r03, r07, r12) chose the same pattern,
mainly for testability and consistency. Run r15 attempted to use
EF directly — that led to test issues and was rolled back.

**Source:** wiki/decisions.md · runs/r15/result.md

**Related topics:** wiki/patterns.md#testing · wiki/known-issues.md
```

### Slack Integration

Query intent requires `@agent-smith` prefix — no bare question-word matching:

```csharp
// Trigger patterns for QueryIntent (require @agent-smith prefix)
@"^@agent-smith\s+(why|what|how|explain|show|which|warum|was|wie|erkläre?|zeig|welche?)\s+.+"
@"^@agent-smith\s+\?\s*.+"    // @agent-smith ? something
```

Low confidence → clarification: "Did you mean a question about my-api?"

---

## p61c: WikiLint — Healthy Wiki

### Health Checks on the Wiki

Periodically (weekly, or explicitly):

```
agent-smith wiki-lint --project my-api
```

LLM analyzes the entire wiki and produces a health report:

```markdown
# Wiki Health Report — my-api
_2026-04-02_

## Inconsistencies Found (3)

- `decisions.md` says "Redis for sessions" but `patterns.md` still mentions
  InMemory Cache — contradiction to be resolved
- `PaymentService.md` describes timeout issue but `known-issues.md`
  has no entry for it — needs synchronization
- r18 result.md describes an auth fix, but `architecture.md`
  doesn't reflect it

## Gaps (2)

- No documentation on deployment process (several runs mention
  "deploy to staging" but wiki doesn't explain how)
- `AuthFlow.md` missing — referenced in 7 runs, no article yet

## New Article Candidates (4)

1. **PaymentService Deep Dive** — 8 runs, many issues, deserves own article
2. **Testing Strategy** — consistent pattern across all runs recognizable
3. **Error Handling Conventions** — same discussions keep recurring
4. **Redis Integration Guide** — pitfalls well documented in r09/r11

## Suggested Next Questions

- "Why didn't we implement CQRS even though it was discussed?"
- "Which technical debts accumulated across the most runs?"
- "Which module had the most regressions?"
```

The lint report is stored in `.agentsmith/wiki/health/` and
delivered via Slack notification.

### Automatic Remediation

With `--fix` flag, WikiLint fixes inconsistencies automatically:

```
agent-smith wiki-lint --project my-api --fix
→ Creates PR with wiki cleanups
```

---

## Directory Structure (Complete)

```
.agentsmith/
  wiki/
    index.md           (master index, backlinks, auto-maintained)
    architecture.md
    patterns.md
    decisions.md       (compiled, structured)
    known-issues.md
    security.md
    concepts/
      *.md             (auto-generated per concept)
    health/
      *.md             (WikiLint reports)
  context.yaml
  code-map.yaml
  coding-principles.md
  runs/
    r01/ r02/ ...
  security/
    *.sarif
```

---

## Steps

### Step 1: Wiki directory schema + `index.md` format
Contracts + schema definition. No code, just specification.

### Step 2: CompileKnowledgeHandler (incremental)
LLM prompt + handler + commit to default branch. Runs after WriteRunResult.

### Step 3: `agent-smith compile-wiki` CLI command
Explicit recompilation. `--full` for complete rebuild.

### Step 4: QueryHandler + QueryIntent parser
Dispatcher extension. No container job. Direct answer.
Sparse checkout for `.agentsmith/wiki/` only.

### Step 5: Slack: query intent routing
`@agent-smith` prefix required. Low-confidence → clarification.

### Step 6: WikiLint + Health Report
`agent-smith wiki-lint`. Slack notification for critical inconsistencies.

### Step 7: WikiLint `--fix` → PR
Automatic remediation as PR.

### Step 8: Tests + docs

---

## Definition of Done

- [ ] Wiki directory created on first `compile-wiki`
- [ ] `CompileKnowledge` runs incrementally, respecting rate limit config
- [ ] Wiki commits go to default branch, not PR branch
- [ ] `agent-smith compile-wiki --full` rebuilds wiki completely
- [ ] `index.md` automatically maintained with backlinks
- [ ] `QueryIntent` recognized by dispatcher (`@agent-smith` prefix required)
- [ ] Query answer in <15 seconds in Slack (sparse checkout)
- [ ] Source reference in every answer
- [ ] `agent-smith wiki-lint` produces health report
- [ ] Health report delivered via Slack
- [ ] `wiki-lint --fix` creates PR with cleanups
- [ ] Wiki survives code changes (no conflict with normal PRs)
- [ ] Unit tests: QueryHandler, CompileKnowledgeHandler
- [ ] Integration test: 3 runs → compilation → query → correct answer

---

## Dependencies

```
p41 (decisions.md)      — wiki has data to compile
p22 (Bootstrap)         — context.yaml as wiki basis
p58 (Dialogue)          — QueryIntent uses IPlatformAdapter
p60c (Git Trend)        — security.md in wiki from SARIF history
```

---

## Why This Matters

Karpathy described this with ~100 articles and ~400k words.
A codebase with 20+ runs, security scans, and decisions.md is
structurally identical — only the domain is code instead of research papers.

The key difference from RAG: no embeddings, no vectors, no black-box retrieval.
Everything is a readable markdown file in Git.
A human can trace every agent statement to its source.

This is also the difference from Devin's "Wiki" feature — Devin's Wiki is
internal, not auditable, not versioned. Agent Smith's Wiki is in Git,
human-readable, and grows with the project.
