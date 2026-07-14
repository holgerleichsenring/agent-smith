# First run

Two steps. Step 1 proves the whole loop in minutes on a bundled sample project — the only credential you need is an LLM key. Step 2 connects your real tracker and repo.

## Step 1 — the demo

`agent-smith demo` materializes a tiny C# project (with a seeded, deterministic bug and a failing unit test that pins the expected behavior) into a local git workspace, files an inline ticket describing the bug, and runs the real `fix-bug` pipeline against it. No tracker, no repo remote, no Docker, no Redis — the result is a local commit plus a printed diff.

The minimal config is just one agent and its key:

```yaml
# agentsmith.yml
agents:
  default-openai:
    type: openai
    models:
      scout:   { model: gpt-4.1-mini }
      primary: { model: gpt-4.1 }
      planning:      { model: gpt-4.1 }
      summarization: { model: gpt-4.1-mini }

secrets:
  openai_api_key: ${OPENAI_API_KEY}
```

```bash
export OPENAI_API_KEY=sk-...
agent-smith demo
```

What happens, in order:

1. **Preflight** — the relevant subset of `agent-smith doctor` (config schema, LLM reachable, sandbox spawn, infra). A broken environment fails here with a fix hint, before any pipeline tokens are spent. Redis is not required: the check reports it as skipped for one-shot CLI runs.
2. **Workspace** — the bundled sample project is extracted to a temp directory and git-initialized with one baseline commit (`--workspace DIR` to choose the location, `--agent NAME` to pick a specific agent from your config).
3. **The run** — the real `fix-bug` preset, headless and in-process: inline ticket → checkout → analyze → plan → agentic execute → test → commit. Same production path your real tickets will take.
4. **The result** — a local commit fixing the seeded bug, the `git diff HEAD~1` printed to your terminal, and the workspace left in place for inspection.

Exit code 0 means the loop worked end to end. Everything after this page is about pointing that same loop at your own systems.

## Step 2 — your real tracker and repo

Walking through one full `fix-bug` run, end to end. The example uses the fictional `TodoList` project. Substitute your tracker, repo, and AI provider as you go — the pages under [Connect your stuff](../connect-your-stuff/tracker-azure-devops.md) have the specifics per system.

### What you need

- Agent Smith installed (see [Install](install.md)).
- A repo Agent Smith can clone. For the walkthrough, anything works — a fresh `TodoList` repo with a couple of `.cs` files and a failing test is enough.
- A ticket in your tracker that describes a bug. For the walkthrough, "Null reference in `UserService.GetById` when `id` is zero".
- An API key for one AI provider. The example uses OpenAI; any of the providers on the [AI providers page](../connect-your-stuff/ai-providers.md) work.

About ten minutes of your time.

### Write the config

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

### Set the secrets

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...
```

The `${...}` references in `agentsmith.yml` resolve from the environment. Don't paste keys into the YAML — Agent Smith will refuse the config if it sees raw secrets.

### Check the wiring, then run

```bash
agent-smith doctor
```

The doctor actively probes everything the run will need — config schema, LLM reachable, tracker auth, repo access, skills catalog, sandbox spawn — and prints a fix hint per failed check. Green means the run below won't die on plumbing.

```bash
agent-smith fix --ticket 54 --project todolist
```

What you'll see (CLI mode, in-process sandbox, no Docker required — abridged, your file names and numbers will differ):

```
[ 1] LoadCatalog           → skills catalog loaded (embedded release build)
[ 2] FetchTicket           → "Null ref in UserService.GetById when id is zero"
[ 3] ScopeRepos            → 1 repo affected: todolist-api
[ 4] CheckoutSource        → branch agentsmith/ticket-54 created in todolist-api
[ 5] BootstrapCheck        → .agentsmith/context.yaml + coding-principles.md present
[ 6] AnalyzeCode           → scout pass: 47 files scanned, 3 candidates
[ 7] NegotiateExpectation  → expectation ratified: GetById(0) → 400 BadRequest,
                             existing behavior for valid ids unchanged
[ 8] GeneratePlan          → 4 steps

   Plan:
   1. Add null-id guard at the top of GetById
   2. Return a 400 BadRequest with a typed problem detail
   3. Add UserService.GetById_ZeroId_ReturnsBadRequest test
   4. Update the OpenAPI spec to declare the 400 response

   Approve this plan? [y/N] y

[ 9] AgenticMaster         → coding-agent-master: code changed, tests written,
                             build + tests verified green inside the sandbox
[10] CommitAndPR           → opened https://github.com/acme-org/todolist-api/pull/142

Done. 1 pull request open. Ticket #54 → resolved. Cost: $0.018.
```

Two steps in there deserve a word. `NegotiateExpectation` writes down WHAT the fix must achieve — grounded in the actual analysis, not the raw ticket text — and that ratified expectation becomes the run's acceptance contract: it drives the plan, the master's prompt, and the PR body. And `AgenticMaster` is one agentic loop that plans details, edits, and runs the repo's own tests itself; there is no separate rigid test step that guesses your test command.

If the ticket is too thin to work from (title-only, no reproduction, contradictory), the run doesn't guess: it posts its open questions as a comment on the ticket, parks the ticket in a `needs_clarification` status, and resumes when you answer. See [Spec dialogue](../how-it-works/spec-dialogue.md).

The CLI exits with code zero if the PR opened and the ticket got updated. Non-zero otherwise, with the failing step in the message.

### What ended up on disk

```
.agentsmith/runs/2026-05-22T14-03-11-9f2a-fix-54/
├── plan.md       — the plan after the planning round, role-by-role
├── result.md     — what got done, the PR URL, the cost
└── decisions.md  — non-obvious choices (e.g. "picked 400 over 404 because the
                    spec already documents 400 for malformed input")
```

Run directories accumulate over time. The [knowledge-base feature](../reference/concepts/knowledge-base.md) compiles them into a wiki you can grep when something feels familiar — "didn't we already debate this trade-off six months ago?" usually has an answer in there.

### What ended up on the tracker

The ticket got:
- Status moved to your `done_status` (in the example, `Closed`).
- A new comment with the PR URL and the run id.
- The `agent-smith:done` lifecycle label.

If the run fails, the ticket gets the `agent-smith:failed` label and a comment with the error. The PR — if any code got committed before the failure — stays open in draft so you can look at it.

### Headless mode

The approval prompt is on by default in interactive CLI mode. To skip it for a single run:

```bash
agent-smith fix --ticket 54 --project todolist --headless
```

Server mode (Docker / k8s) always runs headless — every webhook- or poll-triggered run auto-approves. The interactive gate is a CLI convenience for when you're still building trust.

### Next

- Wire your tracker so tickets trigger runs automatically: [Webhooks](../trigger-it/webhooks.md), [Polling](../trigger-it/polling.md), [Labels](../trigger-it/labels.md).
- Move from CLI to a long-lived host: [Docker Compose](../host-it/docker-compose.md), [Kubernetes](../host-it/kubernetes.md).
- Read [Methodology](../how-it-works/methodology.md) if you want to know why the plan / review / verify phases exist in that order.
