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

### Delivery account eval (`AccountDeliveryEvalTests`, 2026-08-25-7035)

Runs the REAL delivery account over the fixture deliveries under
`Fixtures/AccountDeliveries/` and scores its dispositions against the truth each
fixture declares. Report: `Reports/account-deliveries/`, named per model + account
prompt digest — commit it; the next change to the account has to show up there as a
diff of the same file or it has not been measured.

Two rates, each over its own denominator:

- **false negatives** / criteria the branch genuinely MET — the account refusing
  delivered work, which is what has cost live runs;
- **false positives** / criteria the branch genuinely did NOT meet — the account
  rubber-stamping, which is what the refusal rule exists to prevent.

A fixture is a DELIVERY, not a recording: a base tree and a branch tree, made into
real git repositories at test time and handed to the account through the in-process
sandbox. The account's `search_branch` therefore answers real patterns, including
ones a future prompt invents — which is why the corpus does not go stale when the
prompt changes, and why replaying recorded runs was rejected (since p0483 the
account is a tool-using call; with no sandbox a replay scores a different component).

`AccountEvalMechanicsTests` proves materialisation, the search tool, the scoring
arithmetic and the report shape without a model, so the numbers can be reasoned
about without credentials.

The plan-call eval (`PlanCallLiveEvalTests`, p0397) went with p0415: it
measured the embedded `agent-plan-system` template, and GeneratePlan — the
only caller — is retired. The spec is the plan now.
