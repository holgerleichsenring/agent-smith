# Settings

Below the catalogs in the studio rail sit fourteen global settings groups. Each one is a typed form over a top level block of `agentsmith.yml`, and each one applies to every project unless a project overrides it.

They save the same way the catalogs do: one **Save changes**, a row in the change feed, and a live pickup by the running server, except where a field says it applies after a restart. A value the server would refuse at run time (a negative round count, a series cap above 26) is refused on save instead, and the reason shows under the Save button.

## What each group changes

### Orchestrator

`MaxRunWallTimeSeconds`, the ceiling on how long one run may take before it gets cancelled. It defaults to 1800, so a run that is still going after thirty minutes is stopped. Raise it for a codebase where a legitimate run genuinely takes longer, and lower it if you would rather find out early. A design turn in a conversation is not stopped by it: only its cost cap or a person ends one, and every model call it makes is bounded by the agent's network timeout instead.

### Sandbox

The sandbox agent image (registry and version) plus two timeouts, `StepTimeoutSeconds` at 900 and `RunCommandTimeoutSeconds` at 300. The two are one rule: a `run_command` gets the command timeout when it asks for none, may ask for more, and is killed at the step cap. So the step cap is the ceiling a command may request, and raising it raises what a command may ask for; the command timeout can't be set above it. A repo whose test suite runs longer than five minutes needs the command timeout raised, and the symptom when you haven't is a command that reports it timed out at the same second every time.

Leave the agent version empty and the sandbox agent tag is derived from the release the running server is, so the two can't drift apart by being forgotten. Set it only to pin a different published tag on purpose (an air-gapped mirror carrying one release, say); a pin is reported as an advisory finding, never refused.

The image pin and the two timeouts are read from an instance built at startup, so they need the server restarted; each of those fields says so. The rest of the group applies without one:

- `MaxConcurrentSandboxes`: the Docker capacity probe reads it each time it decides whether a run fits. The field is optional (the same key is `max_concurrent_sandboxes` in `agentsmith.yml`), and left empty it falls back to the `SANDBOX_MAX_CONCURRENT` environment variable, then to the built-in default of 2. `0` means unbounded. It bounds the **Docker** backend only: a Kubernetes installation bounds sandboxes through its namespace `ResourceQuota` and the in-process backend is unbounded. The full story is on the [capacity page](../reference/operations/capacity.md#where-the-concurrent-sandbox-bound-is-set).
- Sandbox hold (seconds): how long a design conversation keeps the source sandboxes it opened after a turn, so the next question doesn't pay for the clone again. Empty falls back to `SANDBOX_HOLD_SECONDS`, then 180; `0` holds nothing. A project can override it on its own sandbox tab.
- **Trusted image registries**: where a context's `stack.image` may come from (`allowed_registries` in `agentsmith.yml`). Each entry is a prefix of the image reference as Docker reads it, so an image with no registry host counts as `docker.io`: `sbtscala/` and `docker.io/sbtscala/` are the same entry, `docker.io/library/` is the Docker Hub official images (`node:22-bookworm`), and `docker.io/` alone trusts all of Docker Hub. With no entry the built-in default applies (`mcr.microsoft.com/`, `ghcr.io/` and `docker.io/library/`), and adding the first entry starts from those three as visible, removable rows, so naming one registry doesn't silently drop the others. **Allow new registries** (`allow_new_registries`) lets a ticket's code run add the registry of an image the list refuses, as a change attributed to that run and recorded in its decisions; a pull-request review or a security scan never adds one. Below it, **In force now** shows the boundary the next decision is judged against, as the server reads it, and says when the form has unsaved changes. A save applies to the next image decision. The boundary judges the `stack.image` a context declares, and a refused one falls back to the language default. An image you name yourself in a project's sandbox tab is your choice and passes as written, and so do the built-in fallbacks.

**Harden sandboxes** (`hardening_enabled` in `agentsmith.yml`, off by default) runs Docker and Kubernetes sandboxes as uid 1000 with no Linux capabilities and no privilege escalation. It needs a server restart. Before turning it on, find the projects whose prerequisites install system packages (`apt-get`, `npm install -g`): they fail until their sandbox tab turns on **run sandboxes as root**, which then shows a warning on each of their runs. [Kubernetes](../host-it/kubernetes.md#what-a-sandbox-pod-runs-as) has the details.

Every one of these except `MaxConcurrentSandboxes`, hardening and the registry boundary can be overridden per project in the project drawer's sandbox tab (see [The Config studio](config-studio.md#the-project-drawer)). Image pull secrets and what the hold does to capacity live with the rest of the sandbox model on [Sandbox architecture](../reference/concepts/sandbox-architecture.md), and the registry boundary from the cluster side on [Kubernetes](../host-it/kubernetes.md#which-registries-a-toolchain-image-may-come-from).

### Deployment

A single registry plus version for the sandbox agent image, used when the Sandbox group leaves its own unset. Left empty, the tag is derived from the running server's release, so there is nothing to bump on upgrade; set it only to pin a tag on purpose.

### Registries

Private package feeds the agent authenticates against inside the sandbox, so `dotnet restore` or `npm install` against your internal feed works without baking credentials into a toolchain image.

### Primary provider

The agent used when a project doesn't name one.

### Limits

The ceilings on one agentic loop: tool calls, tokens, sub agents, concurrent skill calls. These stop a confused loop from grinding, and they apply per skill rather than per run.

### Pipeline cost cap

The money one. A default cap in USD and tokens, five tier caps (trivial, small, medium, large, xl) applied by the estimated size of the work, and optional per pipeline overrides. A run that hits its cap stops and says so. A tier you never edited keeps its built-in default ($1, $2, $8, $25 and $40), and the form shows that value marked as the default; editing one stores only that tier. [Pipeline cost cap](../reference/configuration/pipeline-cost-cap.md) has the token halves and how a tier is picked.

### Queue

Consumer backpressure, and how often the queue retries against Redis.

### Dialogue

How long a run waits for you. `HotWaitSeconds` is the window it holds the sandbox open expecting a fast answer. `ApprovalTimeoutSeconds` is how long the question stays answerable before the run gives up. The defaults are ten minutes and three days.

The same block carries `dashboard_url`, your dashboard's address, so the ticket comment that asks a question links to the run's page. Without it the comment carries no link. It also carries `admin_agent`, the configured agent the admin assistant runs on (Work it out > Agent Smith, admins only). `admin_read_limit` and `admin_result_max_chars` bound what one of its reads returns. All of them have a field in the form.

### Spec review

No step of a run reviews a spec any more: a coding run builds the spec it has, `revise_spec` writes the master's own text without a review (a second opinion is a reviewer the master briefs itself), and a proposal in [Work it out](../how-it-works/work-it-out.md) is not reviewed after its turn. The `spec_review:` block holds one setting:

- Re-ground read budget, 24: the file reads that re-check the citations of the spec a run enters; past it a citation is left unchecked.

The block is `spec_review:` (`reground_read_budget`), and every run reads it fresh. An invalid value is refused at validation: saving it, importing a file with it, and the startup check all name the problem (`spec_review.reground_read_budget is 0; it must be 1 or more`). `design_turn`, `rounds`, `reviewers`, `max_concurrent`, `budget_share_percent`, `attack_look_floor` and `series_contract_max_chars` are retired: a file or a stored setting that still names one gets an advisory, and saving the settings page once clears a stored one.

### Series

How big a ticket's series of specs may get, how much of it one attempt runs, and how far a chain of attempts may go on by itself:

- Max specs, 24: the most specs one ticket's series holds, between 1 and 26 (one member letter each).
- Specs per attempt, 8: the most specs one attempt runs. It can't exceed max specs, nor 18, the most one run's step guard holds.
- Seconds per spec, 0: the measured time of one whole spec block (build, review, verify, record). With a value, an attempt runs fewer specs when the blocks left don't fit the run's wall clock, keeping one block for the commit and PR. `0` means not measured, and specs per attempt alone decides. The server refuses a value where two blocks exceed the Orchestrator's run wall time.
- Budget (USD), 150: what one chain of attempts may spend before the next attempt is held for a person. `0` turns automatic continuation off, so no attempt starts by itself.
- Wall clock (hours of run time), 12: the summed run time of one chain before the next attempt is held. Time spent waiting between attempts doesn't count.
- Continue after a wall-time cancel, on: whether an attempt the wall clock cancelled after a finished spec goes on with the next attempt.

The block is `series:` (`max_specs`, `specs_per_attempt`, `seconds_per_spec`, `budget_usd`, `wall_clock_hours`, `continue_after_wall_time`), read fresh by every run.

### Skills

Where the skill catalog is resolved from. Every release ships with its catalog embedded, so there is nothing to pin here. It exists as an override for skills development and for air gapped mirrors.

### Pipeline data flow

Whether the data flow gate warns or enforces.

### Trace

Whether a run records its conversation (every prompt as sent, every answer, every tool result as the model received it), not only its numbers. Off by default, because a traced run's record grows fast. The `AGENTSMITH_TRACE` environment variable, when it is set, wins over this switch in both directions, so a deployment that sets it in its manifest keeps what the manifest says. See [Recording what the model was told](../connect-your-stuff/ai-providers.md#recording-what-the-model-was-told).

### PoCs

PoCs are small apps people upload to have them hosted. The feature is off until you switch it on. While it's off there is no PoCs item in the sidebar and every PoC route answers 404.

- On: the switch. The other fields can be filled in while it's off.
- Project agent and project tracker: every person gets their own `poc-<person>` project, and a project needs an agent and a tracker to resolve. Both are required while PoCs are on.
- Targets: where PoCs go. One for now. Each names its source repo, one exact repository (a catalog name or `connection/name`, no `*`), and three folders inside it: the root for PoC sources (`pocs`, a PoC lives in `<root>/<project>/<poc>/`), the deploy root (`deploy`) and the template path (`poc-template`). The folders are relative and may not lie inside each other. Also the host pattern with one `{app}` (for example `{app}.poc.example.internal`), the lifetime in days (90), the reminder days before expiry (14, 7, 1) and how many live PoCs one person may hold (3; an admin isn't bound by it).

- Archive: what an uploaded ZIP is held to. The ZIP itself (25 MB), what it unpacks to (100 MB), how many files (2000), how many times its compressed size one file or the whole archive may unpack to (100, which stops a ZIP bomb), the longest path (240 characters, the most the store holds), one file (10 MB), binary files together (20 MB, a file with a NUL byte in its first 8000 bytes counts as binary, checked when the import runs) and the Dockerfile (64 KB). Every refusal names the value and the key it went over.
- Different-app question: an update asks "is this really the same PoC?" when more than 50 percent of the files differ and more than 100 of them do, or when the exposed port or the entrypoint changed. A new base image alone doesn't ask. Both numbers are fields.
- Code scan: every upload is scanned before anything is committed, so a blocked upload never reaches git. A secret pattern of severity high or critical blocks when its catalog confidence is at least the minimum confidence (8); a blocking secret can never be overruled. Below that confidence, generic assignments, placeholders such as `CHANGE_ME` and publishable keys are notes. A dependency vulnerability blocks when its CVSS score is 9.0 or more (no score is a note); one with no fixed version blocks too while "block criticals without a fix" is on (the default). A vulnerability block can be lifted by someone holding `pocs.overrule` with a reason. The dependency scan runs OSV-Scanner from an image you pin by digest (required while PoCs are on; a tag is refused) in a sandbox, so the server needs Kubernetes or Docker; it fetches its databases from `osv-vulnerabilities.storage.googleapis.com`, which the shipped egress allowlist names. Its time limit is 300 seconds.
- Blocked uploads are kept 7 days (a field), then purged.
- Container registry (each target, required): where PoC images live, `<login server>/<repository>/<poc>`. This is a container registry, not the package feeds under Registries. Type (`acr` for now), login server (`<name>.azurecr.io`, `<name>-<hash>.azurecr.io`, or the `.azurecr.us` and `.azurecr.cn` clouds), repository prefix (`pocs`), pull user and pull token. Agent smith holds only a pull-only token: on ACR create a repository-scoped token whose scope map grants pull (`content/read`, `metadata/read`) on `<repository>/*`, put its password in the secrets catalog and pick that secret. Agent smith uses it to turn a tag into a digest and to check that a digest exists; pushing is the host pipeline's job. Pull mode says how the target cluster pulls: "deliver a pull secret" (the default) puts the token into each PoC namespace as an imagePullSecret, "cluster can already pull" relies on the cluster's own access. The token is required either way. Test connection checks the values in the form, PoCs on or off: it says whether the token reaches `<login server>/<repository>`, or that it signs in but its scope map leaves the prefix out. The secret's card lists the PoC target as a user.
- Old images: agent smith deletes nothing in the registry. Schedule a purge there yourself. On ACR that is an ACR Task running `acr purge --filter '<repository>/.*:.*' --ago <N>d` (add `--untagged` to drop untagged manifests too), see Microsoft's [acr purge](https://learn.microsoft.com/azure/container-registry/container-registry-auto-purge) page. ACR's [retention policy](https://learn.microsoft.com/azure/container-registry/container-registry-retention-policy) deletes untagged manifests only, and every PoC image is tagged `<poc>:<sha>`, so it would delete nothing. Make `<N>` longer than the longest PoC lifetime including extensions: a shorter purge removes a running PoC's image, so the next pod restart fails to pull, or the image a rollback needs. Try it with `--dry-run` first.
- Source repo: a passed upload is one commit into `<root>/<project>/<poc>/` on the source repo's default branch. The repo's token needs push to that branch; a protected default branch refuses the commit, and the run names the host's reason.

The block is `pocs:` (`enabled`, `project_agent`, `project_tracker`, `targets` with their `registry`, `archive`, `scan`, `blocked_retention_days`). A value the rules refuse is named on save, on import and at startup; a source repo, agent, tracker or registry pull secret that doesn't exist is a startup finding naming the key. A target saved before the registry block existed is refused until it has one.

Who sees what: `pocs.read` opens the PoCs area, `pocs.write` imports and acts on your own PoCs, `pocs.overrule` lifts a scan block and `pocs.admin` acts on every PoC. The built-in `poc` role holds `pocs.read` and `pocs.write`; operators get `pocs.read`. Which PoCs you see follows the projects you read runs in, so a person bound to their own `poc-` project sees their own PoCs. A blocked revision's page and its overrule both need `runs.read` in the PoC's project, so someone who overrules holds `pocs.overrule` and `runs.read` there (globally, for example the reader role, or as admin); without it the PoC answers like a foreign one.

## What the settings rail leaves out

`persistence:` is absent on purpose. It's bootstrap only, read from the file before the server can talk to a database, so making it editable in a UI backed by that database would be a circle. Change it in `agentsmith.yml` and restart.

`auth:` and `tool_runner:` are bootstrap as well: the token authority is wired before the database is reachable, and the scanners' runner is built at start. Both stay in the server's own `agentsmith.yml`. An import doesn't store them and says so, as it does for every other key of the file it doesn't keep.

`secrets:` is absent because it has its own catalog. The studio holds names, and values stay in the environment.
