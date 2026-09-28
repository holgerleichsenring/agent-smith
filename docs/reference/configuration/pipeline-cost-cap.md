# Pipeline cost cap

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited under **Configuration → Pipeline cost cap** in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. See [Where configuration lives](../../configure-it/index.md).

Limits what a single pipeline run may spend, in USD and in tokens. Without a cap, a runaway run (a large swagger, a loop that keeps dispatching, a model routed wrong) can burn through an unbounded budget. With one, the run stops spending at a number you picked and says so.

## Defaults

```yaml
pipeline_cost_cap:
  default:
    usd: 5
    tokens: 500000
```

If no cap appears in the configuration, these defaults apply.

## Per-pipeline overrides

```yaml
pipeline_cost_cap:
  default:
    usd: 5
    tokens: 500000
  per_pipeline:
    api-security-scan:
      usd: 10
      tokens: 1000000
    code:
      usd: 2
      tokens: 200000
```

Resolution falls back to `default` when the active pipeline isn't in `per_pipeline`. Names match the pipeline presets (`api-security-scan`, `security-scan`, `code`, and so on).

## Tier caps

A run's cap is sized to the work. While scoping, the run estimates a complexity tier for the ticket, and the cap is raised to that tier's cap:

| Tier | USD | Tokens |
|---|---|---|
| Trivial | 1 | 200,000 |
| Small | 2 | 400,000 |
| Medium | 8 | 1,500,000 |
| Large | 25 | 5,000,000 |

Those are the defaults; they're edited under **Per-tier caps** in the same settings form. A tier only ever raises the configured cap, never lowers it, and a run whose tier is unknown keeps the configured cap. The estimate sizes a ceiling and nothing else: whether the work is done is decided by verification.

The dashboard shows a run's cost against its cap and tier, with a budget bar that turns red at the cap.

## Behaviour at the cap

Both arms are checked, and either one tripping exhausts the budget. The token arm counts cache reads at a tenth, since they cost about that; the raw token total would trip on cheap cache traffic while the dollar arm still had room.

Once the budget is exhausted:

- A skill call that hasn't started is skipped. It returns an `Incomplete` outcome with a `cost-cap-exhausted` observation, and the pipeline runs its compile and delivery steps with what it has, so you still see partial output.
- The coding master's current pass stops, and it isn't re-engaged for another pass.
- A phase whose premise check is skipped for money is marked unchecked, not passed.

Every one of these says the same sentence, naming the cap and what was spent in the units the cap compared:

```text
the run's cost cap ($8.00 / 1,500,000 tokens) is exhausted — $8.0412 / 1,502,310 cache-weighted tokens spent, any segment before a park included
```

## A parked run keeps its cap and its spend

A run that parks on a question and resumes later is one run for the budget. The resume carries the tier the cap was sized to and recomputes the cap from it against the configuration as it is now, so a change you made while the run was parked is honoured and the tier still only raises it. It also carries what the run already spent, so the second half doesn't start from zero, and doesn't get a fresh budget either.

## Operator response

If a run stops on its cap and the partial output is unusable, the cap is too tight for that workload: raise the tier caps, the `default`, or the per-pipeline override. If runs hit the cap repeatedly without unusual workload, look at where the money went: the run's [Why this run did that](../operations/dashboard.md#why-this-run-did-that) view breaks the spend down by phase and call.

See [cost tracking](../concepts/cost-tracking.md) for how the spend is priced in the first place.
