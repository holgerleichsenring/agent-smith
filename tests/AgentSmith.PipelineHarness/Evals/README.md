# Eval tier (Category=LiveLLM)

Opt-in, paid-API evals. CI and the fast/docker harness tiers exclude them
(`--filter "Category!=LiveLLM"`); nothing here runs without credentials in the
environment — each suite skips loudly per missing credential.

## Run

```bash
# Everything in the eval tier:
dotnet test tests/AgentSmith.PipelineHarness --filter "Category=LiveLLM"

# One suite:
dotnet test tests/AgentSmith.PipelineHarness --filter "FullyQualifiedName~ExpectationGoldenEvalTests"
```

## Credentials (per client, any subset works)

| Env var | Purpose |
| --- | --- |
| `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT` | Azure OpenAI client (`AZURE_OPENAI_MODEL` optional, default `gpt-4.1`) |
| `OPENAI_API_KEY` | public OpenAI fallback (`OPENAI_MODEL` optional, default `gpt-4.1`) |

## Suites

### Expectation golden eval (`ExpectationGoldenEvalTests`, p0329)

Replays every fixture under `Fixtures/ExpectationGoldens/` through the real
expectation drafter and judges the draft against the human gold per
assertion. Report: `Reports/expectation-goldens/` (deterministic name per
model + skills pin — commit it; its history is the baseline record). Add
fixtures with `ExpectationFixtureIngestion`; the anonymization check gates
both ingestion and load.

The plan-call eval (`PlanCallLiveEvalTests`, p0397) went with p0415: it
measured the embedded `agent-plan-system` template, and GeneratePlan — the
only caller — is retired. The spec is the plan now.
