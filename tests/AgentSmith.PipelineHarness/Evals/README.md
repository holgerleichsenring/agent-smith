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
| *(none)* | **agent CLI** — the scan scoreboards need no key at all: `claude` on PATH is the whole prerequisite. `AGENTSMITH_WORKER_CLI` names a different binary, `AGENTSMITH_WORKER_MODEL` a different model (default `sonnet`). |

### Scan scoreboards (2026-08-28-cc40)

`SecurityCorpusEvalTests` drives the WHOLE `security-scan` preset over a corpus whose
files each declare a verdict, and scores what the run DELIVERED. It is the first
harness path that runs a preset on a real model — every other eval here drives one
component — and it runs on a subscription rather than a paid key, because the
external-worker bridge enters as the model.

Two rates, each over its own denominator:

- **misses** / files that genuinely hold a weakness — a scan that finds nothing looks
  exactly like a clean repository, which is the direction that has cost live runs;
- **false alarms** / files that are genuinely sound — three of them are shaped to look
  suspect on purpose.

Report: `Reports/security-corpus/`, named per model + the packaged `security-master`
digest — commit it. The report LEADS with the sentence that a public corpus cannot grade
the scan; a number without it will be read as a grade.

The scored composition puts four boundaries back (`ScanEvalComposition`): the production
chat-client factory, the EMBEDDED skills catalog (the harness's own roots carry no
`patterns/` at all, so the static scanner would load zero definitions and the score would
be 0/N by construction), the production prompt catalog, and the real finding refuter. The
sandbox is the CLI-mode in-process one over the materialised corpus — no docker needed.

`SecurityCorpusMechanicsTests` proves materialisation, the two rates, the loud skip, the
path matching and the catalog wiring **with no credentials and no agent CLI**, so the
numbers can be reasoned about before anyone spends a call on them.

`ApiCorpusEvalTests` (2026-09-01-6686) is the same shape for `api-security-scan`, against
a target this repository SERVES ITSELF: `StubApiTargetHost` on an ephemeral loopback port,
serving `Fixtures/StubApiTarget/openapi.json` and the behaviour that document describes.
`Fixtures/StubApiTarget/declaration.json` says which endpoints are weak and which are sound
but shaped to look suspect; scoring is by **method and path template**, because an api
finding's location is an endpoint and many carry no file at all. Report:
`Reports/api-corpus/`.

Two things that tier states out loud, because a score that hides them is a wrong score:
the dynamic scanners are STUBBED unless `AGENTSMITH_HARNESS_REAL_SCANNERS=1` and a docker
daemon are present, and any step that was cut off or found nothing is named beside the
number. Findings that name no declared endpoint are reported without a denominator rather
than folded into a rate.

No external machine is needed for either scoreboard, and no docker.

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
