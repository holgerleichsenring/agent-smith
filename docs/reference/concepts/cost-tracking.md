# Cost Tracking

Every run tracks token usage, cost, and duration. Machine-parseable and human-readable. Besides LLM cost, every finished run also shows its **reserved capacity-time** — memory request × pod lifetime, in Gi·minutes — so you see the infrastructure a run held, not just the tokens it burned. See [Capacity](../operations/capacity.md).

## result.md Frontmatter

Each run writes a `result.md` with YAML frontmatter:

```yaml
---
ticket: "#57 — GET /todos returns 500 when database is empty"
project: todo-list
date: 2026-02-24
result: success
duration_seconds: 50
cost:
  total_usd: 0.0682
  phases:
    scout:
      model: claude-haiku-4-5-20251001
      input_tokens: 12450
      output_tokens: 890
      turns: 3
      usd: 0.0062
    primary:
      model: claude-sonnet-4-20250514
      input_tokens: 45200
      output_tokens: 8900
      turns: 7
      usd: 0.0620
---
```

## Per-Phase Breakdown

Cost is tracked per pipeline phase:

- **scout** — the Scout agent that maps the codebase (typically Haiku — fast and cheap)
- **primary** — the main agent that writes code (typically Sonnet — capable and cost-effective)
- **triage** — the specialist selection step
- **skill rounds** — each specialist's contribution

## Pricing Configuration

Model pricing is configured in `agentsmith.yml`:

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  pricing:
    input_per_million: 3.0
    output_per_million: 15.0
    cache_read_per_million: 0.30
```

Dollar cost per LLM call is computed from this `pricing:` block (p0274 made live per-call pricing use the configured block). Without a `pricing:` section you still get token counts — just no USD figures.

## Prompt Caching

Anthropic prompt caching is **on by default**:

```yaml
agent:
  type: Claude
  cache:
    is_enabled: true      # default
    strategy: automatic   # default
```

With caching enabled, Agent Smith stamps cache markers on the system prompt and the tool definitions, so the stable prefix of every agentic call is served from cache.

The cached share of input tokens is recorded **per LLM call** — each call carries fields for cache-read and cache-creation tokens — and the dashboard's per-step cost breakdown shows it. Read it plainly: **a run with a 0% cached share on a caching-enabled provider means the cache is dead. Treat it as an alarm, not a curiosity** (p0323) — something is invalidating the prefix (an unstable system prompt, shuffled tool definitions) and you are paying full price for every call.

Billing semantics differ by provider:

- **Anthropic** — the reported input tokens already exclude cache reads and writes; cache traffic is priced separately via `cache_read_per_million`.
- **OpenAI** — cached tokens are reported inside input tokens and are subtracted from the billable input.

## Pipeline Cost Cap

Each run is bounded by a per-pipeline budget, `pipeline_cost_cap` — default **5 USD / 500k tokens per run**. When the cap is crossed, remaining LLM-driven skill calls short-circuit and the pipeline proceeds straight to compile-and-deliver with what it has, so a runaway loop cannot burn an unbounded budget.

## Local Models

Ollama and other local models show `$0.00` because they're free. That's the point.

```yaml
agent:
  type: Ollama
  model: qwen2.5-coder:32b
  endpoint: http://ollama:11434
  # No pricing section — defaults to $0.00
```

## Querying Cost Data

The YAML frontmatter is machine-parseable with `yq`:

```bash
# Total cost of a run
yq '.cost.total_usd' .agentsmith/runs/r01-fix-login/result.md

# All run costs
for f in .agentsmith/runs/*/result.md; do
  echo "$(basename $(dirname $f)): $(yq '.cost.total_usd' $f)"
done
```
