# First run

Two steps. Step 1 proves the whole loop in minutes on a bundled sample project — the only credential you need is an LLM key. Step 2 connects your real tracker and repo.

## Step 1 — the demo

`agent-smith demo` materializes a tiny C# project (with a seeded, deterministic bug and a failing unit test that pins the expected behavior) into a local git workspace, files an inline ticket describing the bug, and runs the real `code` pipeline against it. No tracker, no repo remote, no Docker, no Redis — the result is a local commit plus a printed diff.

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

```

The agent reads `OPENAI_API_KEY` from the environment; no `secrets:` entry is needed for it.

```bash
export OPENAI_API_KEY=sk-...
agent-smith demo
```

What happens, in order:

1. **Preflight** — the relevant subset of `agent-smith doctor` (config schema, LLM reachable, sandbox spawn, infra). A broken environment fails here with a fix hint, before any pipeline tokens are spent. Redis is not required: the check reports it as skipped for one-shot CLI runs.
2. **Workspace** — the bundled sample project is extracted to a temp directory and git-initialized with one baseline commit (`--workspace DIR` to choose the location, `--agent NAME` to pick a specific agent from your config).
3. **The run** — the real `code` preset, headless and in-process: inline ticket → checkout → analyze → derive the phase spec → coding master → verify → commit. Same production path your real tickets will take.
4. **The result** — a local commit fixing the seeded bug, the `git diff HEAD~1` printed to your terminal, and the workspace left in place for inspection.

Exit code 0 means the loop worked end to end. Everything after this page is about pointing that same loop at your own systems.

## Step 2 — your real tracker and repo

Walking through one full `code` run, end to end. The example uses the fictional `TodoList` project. Substitute your tracker, repo, and AI provider as you go — the pages under [Connect your stuff](../connect-your-stuff/tracker-azure-devops.md) have the specifics per system.

### What you need

- Agent Smith installed (see [Install](install.md)).
- A repo Agent Smith can clone. For the walkthrough, anything works — a fresh `TodoList` repo with a couple of `.cs` files and a failing test is enough.
- A ticket in your tracker that describes a bug. For the walkthrough, "Null reference in `UserService.GetById` when `id` is zero".
- An API key for one AI provider. The example uses OpenAI; any of the providers on the [AI providers page](../connect-your-stuff/ai-providers.md) work.

About ten minutes of your time.

### Write the config

This walkthrough runs from the CLI, which reads its whole configuration from a file. `agentsmith.yml` in the working directory:

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
    url: https://github.com/acme-org/todolist-api    # the repo whose issues are the tickets
    auth: github_token

projects:
  todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist-api]

secrets:
  github_token: ${GITHUB_TOKEN}
```

The shape is catalog-first: top-level `agents:` / `repos:` / `trackers:` define what exists, `projects:` wires them together by name. See [Repos: mono-repo](../connect-your-stuff/repos-mono.md) for the smallest viable shape, [Repos: multi-repo](../connect-your-stuff/repos-multi.md) for the version with three or four sibling repos.

Once you move to a long running server, this same wiring moves into the database and you edit it in the dashboard instead, and the file shrinks to `persistence:` plus `secrets:`. You don't have to redo it by hand: `agent-smith config import ./agentsmith.yml` takes exactly this file. [Where configuration lives](../configure-it/index.md) explains the split.

### Set the secrets

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...
```

The `${...}` references in `agentsmith.yml` resolve from the environment, and each agent reads its provider key from the environment on its own (see [API keys](../connect-your-stuff/ai-providers.md#api-keys)). Don't paste keys into the YAML.

### Check the wiring, then run

```bash
agent-smith doctor
```

The doctor actively probes everything the run will need — config schema, LLM reachable, tracker auth, repo access, skills catalog, sandbox spawn — and prints a fix hint per failed check. Green means the run below won't die on plumbing.

```bash
agent-smith code --ticket 54 --project todolist
```

In CLI mode the sandbox is in-process, so no Docker is required. The run goes through the `code` pipeline:

1. `FetchTicket` reads ticket 54, and `ScopeRepos` decides which of the project's repos it touches (here only `todolist-api`).
2. `CheckoutSource` creates the run branch, then `RunPreflight` proves the preconditions it can only check now that the branch and sandbox exist.
3. `BootstrapCheck` and `BootstrapGate` refuse a repo without `.agentsmith/contexts/<name>/context.yaml`; run `init-project` first (see [Onboarding](../reference/setup/onboarding.md)). Then the principles, the memory index and the context are loaded.
4. `AnalyzeCode` sweeps the repo, and `DeriveSpec` turns the ticket into one or more phase specs, each with a done-list. When the ticket contradicts what is in the repository, `SpecHandback` parks it with a question instead of guessing.
5. For each phase: the coding master edits the code and runs the repo's own build and tests inside the sandbox, then `VerifyPhase` checks the result against the phase's done-list. A red verify stops the run there.
6. `WriteRunResult` writes the run record, and `CommitAndPR` opens the pull request.

The phase spec is the run's acceptance contract. It drives what the master works on, what gets verified, and what the PR body says.

If the ticket is too thin to work from (title-only, no reproduction, contradictory), the run doesn't guess: it posts its open questions as a comment on the ticket, parks the ticket in a `needs_clarification` status, and resumes when you answer. See [Spec dialogue](../how-it-works/spec-dialogue.md).

The CLI exits with code zero if the PR opened and the ticket got updated. Non-zero otherwise, with the failing step in the message.

### What ended up on disk

The run record lands in the repository, under `.agentsmith/runs/<run>/`, and is committed on the run branch so it travels with the PR. `result.md` holds what got done, the PR URL, the cost, the decisions the run logged, and the account of every done criterion.

Run directories accumulate over time. The [knowledge-base feature](../reference/concepts/knowledge-base.md) compiles them into a wiki you can grep when something feels familiar — "didn't we already debate this trade-off six months ago?" usually has an answer in there.

### What ended up on the tracker

The ticket got:
- Status moved to your `done_status` (in the example, `Closed`).
- A new comment with the PR URL and the run id.
- The `agent-smith:done` lifecycle label.

If the run fails, the ticket gets the `agent-smith:failed` label and a comment with the error. The PR — if any code got committed before the failure — stays open in draft so you can look at it.

### Headless mode

`--headless` runs without interactive prompts, which is what you want in cron or CI:

```bash
agent-smith code --ticket 54 --project todolist --headless
```

Server mode (Docker / k8s) always runs headless. There is no plan approval step in either mode: the question a run can't answer goes back to the ticket as a handback instead.

### Next

- Wire your tracker so tickets trigger runs automatically: [Webhooks](../trigger-it/webhooks.md), [Polling](../trigger-it/polling.md), [Labels](../trigger-it/labels.md).
- Move from CLI to a long-lived host: [Docker Compose](../host-it/docker-compose.md), [Kubernetes](../host-it/kubernetes.md). Read [where configuration lives](../configure-it/index.md) first, because a server configures itself differently than the CLI does.
- Read [Methodology](../how-it-works/methodology.md) if you want to know why the plan / review / verify phases exist in that order.
