# Settings

Below the catalogs in the studio rail sit twelve global settings groups. Each one is a typed form over a block that used to be a top-level section of `agentsmith.yml`, and each one applies to every project unless a project overrides it.

![Pipeline cost cap — a default, four tier caps, and per-pipeline overrides](../assets/screenshots/config-settings-costcap.png)

They save the same way the catalogs do: one **Save changes**, a row in the change feed, and a live pickup by the running server.

## What each group changes

**Orchestrator** — the orchestrator container image pin and `MaxRunWallTimeSeconds`, the ceiling on how long one run may take before it's killed. If you've ever had a run wander for two hours, this is the knob.

**Sandbox** — the sandbox-agent image plus two timeouts: per step and per command. A repo whose test suite takes twelve minutes needs the command timeout raised, and the error you get when you don't is a command that dies at the same second every time.

**Deployment** — a single registry plus version that feeds *both* the orchestrator and the sandbox-agent image when the two groups above leave theirs unset. This is the one you bump on upgrade; the other two exist for the case where you want to pin one of them independently.

**Registries** — private package feeds the agent authenticates against inside the sandbox, so `dotnet restore` or `npm install` against your internal feed works without baking credentials into a toolchain image.

**Primary provider** — the agent used when a project doesn't name one.

**Limits** — the ceilings on one agentic loop: tool calls, tokens, sub-agents, concurrent skill calls. These stop a confused loop from grinding, and they're per skill, not per run.

**Pipeline cost cap** — the money one. A default cap in USD and tokens, four tier caps (trivial, small, medium, large) applied by the estimated size of the work, and optional per-pipeline overrides. A run that hits its cap stops and says so.

**Queue** — consumer backpressure and how often the queue retries against Redis.

**Dialogue** — how long a run waits for you. `HotWaitSeconds` is the window it holds the sandbox open expecting a fast answer; `ApprovalTimeoutSeconds` is how long the question stays answerable before the run gives up. The defaults are ten minutes and three days.

**Skills** — where the skill catalog is resolved from. Every release ships with its catalog embedded, so this is an override for skills development or an air-gapped mirror, and most deployments never touch it.

**Pipeline storage** — how long in-flight run artifacts stay in Redis.

**Pipeline data flow** — whether the data-flow gate warns or enforces.

## Two things the settings rail leaves out

`persistence:` isn't here on purpose. It's bootstrap-only — the server reads it from the file before it can talk to a database, so making it editable in a UI backed by that database would be a circle. Change it in `agentsmith.yml` and restart.

`secrets:` isn't here either, because it has its own catalog. The studio holds names; values stay in the environment.
