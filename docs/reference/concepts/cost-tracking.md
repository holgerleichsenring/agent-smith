# Cost tracking

Every run tracks token usage, cost and duration, machine-parseable and human-readable. Besides LLM cost, every finished run also shows its **reserved capacity-time** (memory request × pod lifetime, in Gi·minutes), so you see the infrastructure a run held and not just the tokens it burned. See [Capacity](../operations/capacity.md).

## result.md frontmatter

Each run writes a `result.md` into `.agentsmith/runs/<run-id>/` with YAML frontmatter:

```yaml
---
ticket: "#57 — Fix GET /todos returning 500 when the database is empty"
date: 2026-02-24
result: success
type: fix
duration_seconds: 412
run_id: 2026-02-24T09-14-02-40fa
pipeline_name: code
tokens:
  input: 45200
  output: 8900
  cache_read: 210400
  total: 264500
cost:
  total_usd: 0.3120
  phases:
    Plan:
      model: claude-sonnet-4-20250514
      input: 12450
      output: 2100
      cache_read: 48000
      usd: 0.0832
    Implementation:
      model: claude-sonnet-4-20250514
      input: 32750
      output: 6800
      cache_read: 162400
      usd: 0.2288
---
```

`phases` are the execution phases the calls ran under (`Plan`, `Implementation`, `Verify`, `Review` and so on). Calls made outside any phase land in `Other`, so the rows always add up to `total_usd`. Each row names the model that actually ran and is priced call by call at that model's price, so a run that mixes models reconciles too.

A multi-repo run adds a `repo_cost:` block to each repo's `result.md` with that repo's share, next to the pipeline total.

## Pricing

Dollar cost per call comes from a price per model. Agent Smith carries built-in prices for current Claude models and a few OpenAI ones, matched by prefix so a dated snapshot id finds its alias. An agent's `pricing:` block overrides or extends that table:

```yaml
agents:
  claude-default:
    type: claude
    model: claude-sonnet-4-20250514
    pricing:
      models:
        claude-sonnet-4-20250514:
          input_per_million: 3.0
          output_per_million: 15.0
          cache_read_per_million: 0.30
```

Cache writes are priced at 1.25× the input rate, Anthropic's premium for the five-minute cache.

The configuration studio insists on it: an agent whose role models (scout, primary, planning and the rest) name a model with no entry in the agent's pricing table is refused at save, with "has no pricing entry — add it to the agent's pricing table".

A model with no price anywhere doesn't quietly cost $0. Its tokens are counted per model and the run's cost is marked as a lower bound:

```yaml
cost:
  total_usd: 0.0412
  cost_incomplete: true
  unpriced_tokens:
    my-private-model: 184220
```

If you see `cost_incomplete`, the dashboard's figure undershoots what the provider will bill. Add the model to the agent's pricing table.

## Worker calls

When calls are answered by an external agent CLI instead of a provider API, the run records what that CLI reported, in its own block:

```yaml
cost:
  total_usd: 0.0000
  worker_cli:
    model: claude-sonnet-4-5
    calls: 14
    input: 18200
    output: 9400
    cache_read: 402000
    cache_create: 61000
    reported_usd: 1.8820
```

It's deliberately not part of `total_usd` and never counts against the run's cost cap, because that transport spends nothing against an agent's budget. It's the CLI's own number and isn't comparable to a provider call: its cache-creation tokens are the CLI's own system prompt and tool schemas, charged on every call.

## Prompt caching

Anthropic prompt caching is on by default:

```yaml
agents:
  claude-default:
    type: claude
    cache:
      is_enabled: true      # default
      strategy: automatic   # default
```

With caching on, Agent Smith stamps cache markers on the system prompt and the tool definitions, so the stable prefix of every agentic call is served from cache.

The cached share of input tokens is recorded per LLM call, and the dashboard's per-step cost shows it. Read it plainly: **a run with a 0% cached share on a caching-enabled provider means the cache is dead. Treat it as an alarm, not a curiosity.** Something is invalidating the prefix (an unstable system prompt, shuffled tool definitions) and you are paying full price for every call.

Billing semantics differ by provider:

- **Anthropic**: the reported input tokens already exclude cache reads and writes; cache traffic is priced separately.
- **OpenAI**: cached tokens are reported inside input tokens and are subtracted from the billable input.

## Pipeline cost cap

Each run is bounded by a budget in USD and tokens, `pipeline_cost_cap`, 5 USD / 500k tokens by default and raised by the estimated size of the work. The token side counts cache reads at a tenth. What happens at the cap, and how a parked run keeps its cap and its spend across the park, is on [Pipeline cost cap](../configuration/pipeline-cost-cap.md).

## Local models

A local model costs nothing, but Agent Smith doesn't assume that: give it a zero price, or its tokens are reported as unpriced and the run as `cost_incomplete`.

```yaml
agents:
  local:
    type: ollama
    model: qwen2.5-coder:32b
    endpoint: http://ollama:11434
    pricing:
      models:
        qwen2.5-coder:32b:
          input_per_million: 0.0
          output_per_million: 0.0
```

## Querying cost data

The frontmatter is machine-parseable with `yq`:

```bash
# Total cost of a run
yq --front-matter=extract '.cost.total_usd' .agentsmith/runs/2026-02-24T09-14-02-40fa/result.md

# All run costs
for f in .agentsmith/runs/*/result.md; do
  echo "$(basename $(dirname $f)): $(yq --front-matter=extract '.cost.total_usd' $f)"
done
```
