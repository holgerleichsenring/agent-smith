# Cost Tracking

Every run tracks token usage, cost, and duration. Machine-parseable and human-readable.

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
