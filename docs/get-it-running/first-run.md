# First run

Walking through one full `fix-bug` run, end to end. The example uses the fictional `TodoList` project. Substitute your tracker, repo, and AI provider as you go — the pages under [Connect your stuff](../connect-your-stuff/tracker-azure-devops.md) have the specifics per system.

## What you need

- Agent Smith installed (see [Install](install.md)).
- A repo Agent Smith can clone. For the walkthrough, anything works — a fresh `TodoList` repo with a couple of `.cs` files and a failing test is enough.
- A ticket in your tracker that describes a bug. For the walkthrough, "Null reference in `UserService.GetById` when `id` is zero".
- An API key for one AI provider. The example uses OpenAI; any of the providers on the [AI providers page](../connect-your-stuff/ai-providers.md) work.

About ten minutes of your time.

## Write the config

`agentsmith.yml` in the working directory:

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-openai:
    type: openai
    models:
      scout:   { model: gpt-4.1-mini }
      primary: { model: gpt-4.1 }
      planning:      { model: gpt-4.1 }
      summarization: { model: gpt-4.1-mini }

repos:
  todolist-api:
    type: github
    url: https://github.com/acme-org/todolist-api
    auth: github_token

trackers:
  acme-issues:
    type: github
    organization: acme-org
    auth: github_token

projects:
  todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist-api]

secrets:
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

The shape is the catalog-first one introduced in p0139: top-level `agents:` / `repos:` / `trackers:` define what exists, `projects:` wires them together by name. See [Repos: mono-repo](../connect-your-stuff/repos-mono.md) for the smallest viable shape, [Repos: multi-repo](../connect-your-stuff/repos-multi.md) for the version with three or four sibling repos.

## Set the secrets

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...
```

The `${...}` references in `agentsmith.yml` resolve from the environment. Don't paste keys into the YAML — Agent Smith will refuse the config if it sees raw secrets.

## Run

```bash
agent-smith fix "#54 in todolist"
```

What you'll see (CLI mode, in-process sandbox, no Docker required):

```
[ 1] FetchTicket          → "Null ref in UserService.GetById when id is zero"
[ 2] CheckoutSource       → branch agentsmith/ticket-54 created in todolist-api
[ 3] BootstrapCheck       → .agentsmith/context.yaml + coding-principles.md present
[ 4] LoadContext          → project context loaded
[ 5] LoadCodingPrinciples → loaded
[ 6] AnalyzeCode          → scout pass: 47 files scanned, 3 candidates
[ 7] Triage               → backend-dev + tester selected
[ 8] GeneratePlan         → 4 steps, consensus after round 1

   Plan:
   1. Add null-id guard at the top of GetById
   2. Return a 400 BadRequest with a typed problem detail
   3. Add UserService.GetById_ZeroId_ReturnsBadRequest test
   4. Update the OpenAPI spec to declare the 400 response

   Approve this plan? [y/N] y

[ 9] AgenticExecute       → UserService.cs, ProblemDetails.cs modified
[10] RunReviewPhase       → reviewer agrees, no blocking observations
[11] RunVerifyPhase       → build green, lint clean
[12] Test                 → 47 tests pass, 1 new test added
[13] CommitAndPR          → opened https://github.com/acme-org/todolist-api/pull/142
[14] WriteRunResult       → run directory: .agentsmith/runs/2026-05-22T14-03-11-9f2a-fix-54/

Done. 1 pull request open. Ticket #54 → resolved. Cost: $0.018.
```

The CLI exits with code zero if the PR opened and the ticket got updated. Non-zero otherwise, with the failing step in the message.

## What ended up on disk

```
.agentsmith/runs/2026-05-22T14-03-11-9f2a-fix-54/
├── plan.md       — the plan after the planning round, role-by-role
├── result.md     — what got done, the PR URL, the cost
└── decisions.md  — non-obvious choices (e.g. "picked 400 over 404 because the
                    spec already documents 400 for malformed input")
```

Run directories accumulate over time. The [knowledge-base feature](../reference/concepts/knowledge-base.md) compiles them into a wiki you can grep when something feels familiar — "didn't we already debate this trade-off six months ago?" usually has an answer in there.

## What ended up on the tracker

The ticket got:
- Status moved to your `done_status` (in the example, `Closed`).
- A new comment with the PR URL and the run id.
- The `agent-smith:done` lifecycle label.

If the run fails, the ticket gets the `agent-smith:failed` label and a comment with the error. The PR — if any code got committed before the failure — stays open in draft so you can look at it.

## Headless mode

The approval prompt in step 8 above is on by default in CLI mode. To skip it for a single run:

```bash
agent-smith fix "#54 in todolist" --auto-approve
```

For server mode (Docker / k8s), approval is on or off per project in `agentsmith.yml`:

```yaml
projects:
  todolist:
    # ...
    auto_approve: true   # default false
```

Same code path either way — the gate is just a config flag.

## Next

- Wire your tracker so tickets trigger runs automatically: [Webhooks](../trigger-it/webhooks.md), [Polling](../trigger-it/polling.md), [Labels](../trigger-it/labels.md).
- Move from CLI to a long-lived host: [Docker Compose](../host-it/docker-compose.md), [Kubernetes](../host-it/kubernetes.md).
- Read [Methodology](../how-it-works/methodology.md) if you want to know why the plan / review / verify phases exist in that order.
