# Eval tier (Category=LiveLLM)

Opt-in, paid-API evals. CI and the fast/docker harness tiers exclude them
(`--filter "Category!=LiveLLM"`); nothing here runs without credentials in the
environment — each suite skips loudly per missing credential.

## Run

```bash
# Everything in the eval tier:
dotnet test tests/AgentSmith.PipelineHarness --filter "Category=LiveLLM"

# One suite:
dotnet test tests/AgentSmith.PipelineHarness --filter "FullyQualifiedName~PlanCallLiveEvalTests"
dotnet test tests/AgentSmith.PipelineHarness --filter "FullyQualifiedName~ExpectationGoldenEvalTests"
```

## Credentials (per client, any subset works)

| Env var | Purpose |
| --- | --- |
| `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT` | Azure OpenAI client (`AZURE_OPENAI_MODEL` optional, default `gpt-4.1`) |
| `OPENAI_API_KEY` | public OpenAI fallback (`OPENAI_MODEL` optional, default `gpt-4.1`) |
| `ANTHROPIC_API_KEY` | Anthropic client — plan-call eval only (`AGENTSMITH_EVAL_CLAUDE_MODEL` optional, default `claude-sonnet-5`) |

## Suites

### Expectation golden eval (`ExpectationGoldenEvalTests`, p0329)

Replays every fixture under `Fixtures/ExpectationGoldens/` through the real
expectation drafter and judges the draft against the human gold per
assertion. Report: `Reports/expectation-goldens/` (deterministic name per
model + skills pin — commit it; its history is the baseline record). Add
fixtures with `ExpectationFixtureIngestion`; the anonymization check gates
both ingestion and load.

### Plan-call eval (`PlanCallLiveEvalTests`, p0397)

Answers, without a live pipeline run: does the multi-repo GeneratePlan call
fit the output cap, parse, and cover both repos? Composes the REAL plan
prompts (production `AgentPromptBuilder` + embedded `agent-plan-system`
template, two synthetic repo code maps) around a migration-manual-sized
ticket and calls every available client at `MaxOutputTokens` 8192 and 16384
— the live failure mode being pinned: a large 2-repo ticket fills 8192
exactly and the truncated JSON parses to 0 steps.

Ticket text source:

- `AGENTSMITH_EVAL_TICKET_FILE=/path/to/ticket.html` — a REAL ticket export
  (HTML or plain text; tags are stripped). Never commit that file or the
  report generated from it: step descriptions may echo customer identifiers.
- unset — falls back to the committed synthetic fixture
  `Fixtures/PlanGoldens/synthetic-two-repo-migration.md`.

Report: `Reports/plan-goldens/plan-call-eval-<source>.{md,json}` — one row
per model × cap (finish reason, output tokens, parsed/salvaged steps, both-
repos coverage). Commit only the synthetic-fixture report. Assertions:
mechanics always (a row per combo, report written); quality gate = at least
one combo yields a parsed plan with >0 steps covering both repos.

Committed fixtures under `Fixtures/PlanGoldens/` must pass
`PlanGoldenFixtureAnonymizationTests` (fast tier, runs everywhere).
