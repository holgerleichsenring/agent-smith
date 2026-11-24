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
- Code scan: every upload is scanned before anything is committed, so a blocked upload never reaches git. A secret pattern of severity high or critical blocks when its catalog confidence is at least the minimum confidence (8); a blocking secret can never be overruled. Below that confidence, generic assignments, placeholders such as `CHANGE_ME` and publishable keys are notes. A dependency vulnerability blocks when its CVSS score is 9.0 or more (no score is a note); one with no fixed version blocks too while "block criticals without a fix" is on (the default). A vulnerability block can be lifted by someone holding `pocs.overrule` with a reason. The dependency scan runs OSV-Scanner (by default `ghcr.io/google/osv-scanner:v2.2.4`, pinned by its digest; set the image only to run your own mirror, pinned by digest, a tag is refused) in a sandbox, so the server needs Kubernetes or Docker; it fetches its databases from `osv-vulnerabilities.storage.googleapis.com`, which the shipped egress allowlist names. Its time limit is 300 seconds.
- Blocked uploads are kept 7 days (a field), then purged.
- Container registry (each target, required): where PoC images live, `<login server>/<repository>/<poc>`. This is a container registry, not the package feeds under Registries. Type (`acr` for now), login server (`<name>.azurecr.io`, `<name>-<hash>.azurecr.io`, or the `.azurecr.us` and `.azurecr.cn` clouds), repository prefix (`pocs`), pull user and pull token. Agent smith holds only a pull-only token: on ACR create a repository-scoped token whose scope map grants pull (`content/read`, `metadata/read`) on `<repository>/*`, put its password in the secrets catalog and pick that secret. Agent smith uses it to turn a tag into a digest and to check that a digest exists; pushing is the host pipeline's job. Pull mode says how the target cluster pulls: "deliver a pull secret" (the default) puts the token into each PoC namespace as an imagePullSecret, "cluster can already pull" relies on the cluster's own access. The token is required either way. Test connection checks the values in the form, PoCs on or off: it says whether the token reaches `<login server>/<repository>`, or that it signs in but its scope map leaves the prefix out. The secret's card lists the PoC target as a user.
- Image build (each target): the image is built, scanned and pushed by a pipeline of the source repo's host, never where agent smith runs. Install (in the target's section, `pocs.admin`, PoCs on) starts a run that writes the pipeline into the source repo's default branch once: `.github/workflows/poc-build.yml` on GitHub, `<template path>/azure-pipelines.poc-build.yml` on Azure DevOps, `.gitlab-ci.yml` at the root on GitLab. It refuses to overwrite such a file it did not write, and writes nothing when the file is already current. The names, the job timeouts and the Trivy image are written into the pipeline, so install again after changing them. The pipeline has three jobs: build (the folder at the commit, no registry credential, the image saved as a gzip artifact kept one day), scan (Trivy, by default `aquasec/trivy:0.76.2` pinned by its digest, or your own mirror pinned by digest under the scan settings, on that artifact; a vulnerability blocks at CVSS 9.0 or more by any source's v3 score, Trivy's own severity label decides nothing; a secret blocks when Trivy rates it high or critical and its rule maps to a catalog pattern of at least the minimum confidence; a vulnerability overruled at the code stage is ignored here too), and push (the only job that can reach the registry credential; it reports the pushed digest, which agent smith checks against the registry). A build is followed every 15 seconds up to queue + build + scan + push minutes (3 + 12 + 5 + 3); that sum times 60, plus the code scan's time limit, the rollout deadline and the commit allowance, must stay below the PoC run wall time (see Rollout below), or loading refuses it. A scan report larger than 1024 KB fails the build naming the field.
  - GitHub: the repo token needs `actions: write` and the `workflow` scope (it writes a workflow file), and the host must answer a workflow dispatch with the run it started (`return_run_details`; github.com does, an older GitHub Enterprise Server answers 204 and every build fails saying so). Create the environment `poc-push` with the secrets `POC_REGISTRY_USER` and `POC_REGISTRY_PASSWORD` (an ACR token with push on `<repository>/*`). Artifacts count against your plan's storage.
  - Azure DevOps: the token needs Build read & execute. Create the pipeline `poc-build` once from the YAML the install wrote, create a Docker registry service connection `poc-registry` and permit the pipeline to use it once — otherwise the first build waits for that approval until it times out. The build fetches the commit with the job's own access token. Artifacts follow the build's retention.
  - GitLab: the token needs `api`, the token's role must be allowed to set pipeline variables (a project restricting them answers 400/403), and the push variables `POC_REGISTRY_USER` and `POC_REGISTRY_PASSWORD` are protected and scoped to the environment `poc-push`, which only the push job declares. Build and push use a `docker:dind` service, so the runner needs `privileged = true` (gitlab.com's hosted runners have it). Self-managed GitLab limits artifacts to 100 MB by default; a larger image tar fails the build job with GitLab's words.
  - The pipeline runs uploaded Dockerfiles: run it on the host's ephemeral hosted agents. A self-hosted pool is your risk.
- Old images: agent smith deletes nothing in the registry. Schedule a purge there yourself. On ACR that is an ACR Task running `acr purge --filter '<repository>/.*:.*' --ago <N>d` (add `--untagged` to drop untagged manifests too), see Microsoft's [acr purge](https://learn.microsoft.com/azure/container-registry/container-registry-auto-purge) page. ACR's [retention policy](https://learn.microsoft.com/azure/container-registry/container-registry-retention-policy) deletes untagged manifests only, and every PoC image is tagged `<poc>:<sha>`, so it would delete nothing. Make `<N>` longer than the longest PoC lifetime including extensions: a shorter purge removes a running PoC's image, so the next pod restart fails to pull, or the image a rollback needs. Try it with `--dry-run` first.
- Secrets (each target): how a PoC's secret variables and its pull secret reach the cluster. The provider is Sealed Secrets (Bitnami), the one so far. Paste the controller's public certificate, which `kubeseal --fetch-cert` prints (it is public, so it is a plain field), or name an https URL where the controller serves it (for example its `/v1/cert.pem` behind an ingress) and press Fetch; the form then shows the certificate's SHA-256 fingerprint, when it expires and how old it is. Agent smith seals every value in process to that certificate, in strict scope for the PoC's namespace `poc-<name>`, the way `kubeseal` does, so plaintext never lands in git and only the controller in that cluster can open it. A PoC with a secret variable, or a target with pull mode "deliver a pull secret", needs the certificate before it deploys; the deploy run refuses without it, naming the field. When the controller's key is renewed, paste the new certificate: each PoC is sealed again at its next deploy.
- Pull secret: with pull mode "deliver a pull secret", each deploy seals the pull-only token as a `kubernetes.io/dockerconfigjson` secret `poc-pull` (entry `.dockerconfigjson`, for the login server and pull user) into the PoC's namespace; the pod names it under `imagePullSecrets`. Whether anything changed is told by a fingerprint keyed from the secret store's master key, so this mode needs a master key (`AGENTSMITH_SECRET_MASTER_KEY`): without one, a target in this mode is a blocking finding and every deploy of it is refused. Use "cluster can already pull" where the cluster has its own access.
- PoC variables: entered on the import page (and later on the PoC). A secret variable is stored encrypted in the secret store under the PoC's own scope (never in the catalog, never shown again, not even to its owner), must be at least 6 characters so it can be masked, and is masked in every run of the PoC's `poc-<person>` project — no other project's run sees it. A plain variable has no minimum, is shown back, and is committed in plain text as the ConfigMap `poc-config`, so never put a secret there. Names are environment-variable names (`^[A-Z_][A-Z0-9_]*$`, at most 200 characters); both kinds reach the container as environment variables, and a change applies at the PoC's next deploy, restarting the pod. A PoC holds at most 64 variables of at most 4 KB each (both fields), because a Kubernetes Secret or ConfigMap holds at most 1 MiB. A secret variable needs a master key; writing one without it is refused.
- Source repo: a passed upload is one commit into `<root>/<project>/<poc>/` on the source repo's default branch. The repo's token needs push to that branch; a protected default branch refuses the commit, and the run names the host's reason.
- Ingress (each target): the ingress class your controller answers to (required while PoCs are on) and, optionally, a cert-manager ClusterIssuer; with an issuer every PoC's Ingress gets a TLS certificate in the secret `app-tls`.
- Auth (each target): every PoC is behind auth. The ingress annotations are set on every PoC's Ingress as written, and at least one is required while PoCs are on. They tell your ingress controller to ask your auth proxy before it forwards a request (forward auth in annotation mode). See [The app template](#the-app-template) for an example.
- Network (each target): what each PoC's NetworkPolicy lets through. Requests come only from the ingress controller's namespace (required while on). Traffic goes out only to the cluster DNS (`kube-system`, pods labelled `k8s-app: kube-dns`, port 53) and to the egress proxy. The egress proxy has a URL (required, http or https), which every PoC container gets as `HTTP_PROXY` and `HTTPS_PROXY`, and a pod port (3128, the proxy pod's port, not its Service's, because a NetworkPolicy sees the pod port). Name where it runs either by namespace and pod labels or by a CIDR, exactly one of the two. `no_proxy` (`localhost,127.0.0.1,.svc,.cluster.local`) becomes `NO_PROXY`.
- Quota and container (each target): the ResourceQuota of each PoC's namespace (cpu 1, memory 1Gi, ephemeral storage 2Gi, 4 pods) and the container's requests and limits (cpu 50m/500m, memory 128Mi/512Mi, ephemeral storage limit 256Mi), as Kubernetes quantities. The quota must hold at least twice each container limit, and at least 2 pods, because a rolling update runs the old and the new pod side by side. A quantity that doesn't parse is refused.
- GitOps commit: how often one commit is written again when other commits moved the branch in between (3), and the most files a deploy folder or the app template may hold (500); a folder with more is refused rather than cleaned up in one huge commit.
- Argo CD (each target, required while PoCs are on): the Argo CD that deploys the target's PoC folders. See [PoC targets on Argo CD](#poc-targets-on-argo-cd) for what it needs.
  - URL: the Argo CD server, https only, because the role token is sent to it.
  - Role token: the secret holding the JWT of a role on the AppProject. The dashboard never shows it; Test connection reads its expiry on the server.
  - AppProject, namespace (`argocd`) and destination server (`https://kubernetes.default.svc`): the project the ApplicationSet and every PoC Application belong to, the namespace Argo CD keeps them in, and the cluster the PoC namespaces are created on.
  - Repository URL: the source repo as Argo CD's repository credential names it.
  - ApplicationSet name: empty means `pocs-<target name>`.
  - Warn before the token expires (14 days): a role token expires silently and every PoC run then fails; two weeks leaves time to mint and store a new one.
  - Generator requeue (30 seconds, 10 to 180): the git generator creates and deletes Applications only when it requeues; its default of 180 seconds would spend a third of the rollout deadline.
  - Test connection checks the values in the form, PoCs on or off: the values, the token's secret, the token's expiry (no expiry, the date, or a warning inside the warning days), the host, whether Argo CD takes the token, and the target's ApplicationSet as it is on Argo CD now (present, or absent: run Install).
- Rollout: how a PoC run follows Argo CD after the GitOps commit. Argo CD is read every 10 seconds (1 to 60); each reading is one line on the run's Rollout page (sync, health, template image, checksum, generation, replicas). A rollout that isn't healthy after 600 seconds (at least 60) fails the run. The end margin (60 seconds, at least 30) is kept free at the end of the run, because the step must write its failure and the PoC state before the wall-time cancel and its 30-second kill grace land. The commit allowance (120 seconds, at least 0) is the time sealing and the GitOps commit get between the build and the follow.
- Readiness path length (256 characters, at least 1): the longest readiness path an owner may set on a PoC's detail page — a probe path, not a payload.
- PoC run wall time (2700 seconds): every PoC run is held to it, by both wall-time enforcers, instead of `orchestrator.max_run_wall_time_seconds`. Loading checks that it fits the slowest run: (queue + build + scan + push minutes) × 60 + `scan.timeout_seconds` + `rollout.deadline_seconds` + `rollout.commit_allowance_seconds` must stay below it. With the defaults that is (3 + 12 + 5 + 3) × 60 + 300 + 600 + 120 = 2400, below 2700. A target that goes over is a blocking finding naming the keys.

The block is `pocs:` (`enabled`, `project_agent`, `project_tracker`, `targets` with their `registry`, `build`, `secrets`, `ingress`, `auth`, `network`, `quota`, `container` and `argo`, `archive`, `scan`, `variables`, `gitops`, `rollout`, `run_wall_time_seconds`, `blocked_retention_days`, `readiness_path_max_chars`). A value the rules refuse is named on save, on import and at startup; a source repo, agent, tracker or registry pull secret that doesn't exist is a startup finding naming the key. A target saved before the registry block existed is refused until it has one.

Who sees what: `pocs.read` opens the PoCs area, `pocs.write` imports and acts on your own PoCs, `pocs.overrule` lifts a scan block and `pocs.admin` acts on every PoC. The built-in `poc` role holds `pocs.read` and `pocs.write`; operators get `pocs.read`. Which PoCs you see follows the projects you read runs in, so a person bound to their own `poc-` project sees their own PoCs. A blocked revision's page and its overrule both need `runs.read` in the PoC's project, so someone who overrules holds `pocs.overrule` and `runs.read` there (globally, for example the reader role, or as admin); without it the PoC answers like a foreign one.

A PoC's detail page starts its lifecycle acts, each a run in the PoC's `poc-` project that changes only the desired state on the PoC's row and commits it to the deploy folder: Stop (0 replicas, from ready or failed), Start (1 replica, from stopped), Roll back (to an earlier revision that rolled out healthy and whose image is still in the registry), Apply (the current state again, with a new readiness path or TCP) and Delete (the deploy folder is removed, Argo CD prunes the PoC, the source stays; you confirm by typing the name). A settled delete frees the PoC's name and host, so a new PoC may take them; its namespace may still be terminating for a moment, which Argo CD's self-heal retries. A variable changed on a deployed PoC is applied at once by an Apply run. Acting needs `pocs.write` and `runs.read` by a binding of the PoC's project (or `pocs.admin`).

#### The app template

Each PoC is deployed from one app template in the source repo. The target's Install writes the default files once, into `<template_path>/app/`: `namespace.yaml`, `resourcequota.yaml`, `deployment.yaml`, `service.yaml`, `ingress.yaml` and `networkpolicy.yaml`. A file the branch already holds is never overwritten, so from then on they are yours to edit. The Install fills the target's quota, container resources and network keys into these files; after that, a change to those keys reaches an installed target only when you edit the template (or delete a file and install again).

Every GitOps commit of a PoC reads the template at the branch head, renders it into `<deploy_root>/<poc>/` and commits the result straight onto the source repo's default branch, with no pull request. Only `*.yaml` and `*.yml` files are rendered; anything else is skipped and the step says so. A file may hold several documents, each one resource. A template edit reaches each PoC at its next GitOps commit; agent smith does not re-render every PoC when the template changes. Argo CD applies the folder through the target's ApplicationSet, see [PoC targets on Argo CD](#poc-targets-on-argo-cd).

Placeholders are plain string substitution:

| Placeholder | Becomes |
|---|---|
| `{app}` | the PoC's name |
| `{namespace}` | `poc-<name>` |
| `{host}` | the target's `domain_pattern` with the name for `{app}` |
| `{port}` | the port of the revision being deployed (a rollback renders that revision's port) |

The default template double-quotes every placeholder (`"{app}"`), so a PoC named `123` stays a string. Keep that when you edit it.

Agent smith sets some fields itself, whatever the template says. On the Deployment named `app`:

- `replicas`, as an integer.
- pod labels `app.kubernetes.io/name: app` and `agent-smith.io/poc: <name>` (the Service selector and the NetworkPolicy's pod selector match them).
- the Deployment's annotation `argocd.argoproj.io/sync-wave: "1"`. Everything else, the sealed secrets included, stays in wave 0, so they are applied and healthy before the pod starts.
- container `app`: the image by digest; a readiness probe, `httpGet` on the PoC's readiness path or `tcpSocket` on the port when it has none; `envFrom` the ConfigMap `poc-config`, plus the Secret `poc-secrets` only when the PoC has a secret variable; `HTTP_PROXY`, `HTTPS_PROXY`, `http_proxy` and `https_proxy` from `network.egress_proxy.url`, and `NO_PROXY` and `no_proxy` from its `no_proxy`.
- `imagePullSecrets: [poc-pull]` when the pull mode is "deliver a pull secret".
- the pod annotation `agent-smith.io/variables-checksum`, so a variable change restarts the pod.

On the Ingress named `app`: `ingressClassName`, the host, the TLS secret `app-tls` with the annotation `cert-manager.io/cluster-issuer` when `ingress.tls_issuer` is set, and every entry of `auth.ingress_annotations`.

Agent smith also writes `configmap.yaml`: the ConfigMap `poc-config` with one entry per plain variable. The sealed `poc-secrets.sealed.yaml` and `poc-pull.sealed.yaml` come from the seal step; when nothing is left to seal they are removed from the folder.

Quoting: every string value agent smith writes (labels, annotations, ConfigMap data, env values) is double-quoted whenever it would otherwise read as a number, a boolean or null. A variable `DEBUG=true` or `PORT=8080` reaches the pod as a string.

A render must pass this policy, or the deploy is refused naming the file and the field. You can edit the template; the hardening is not optional.

- Every container and initContainer: `runAsNonRoot: true`, `runAsUser: 10001`, `readOnlyRootFilesystem: true`, `allowPrivilegeEscalation: false`, `capabilities.drop: [ALL]`, and cpu and memory limits.
- The pod spec: `automountServiceAccountToken: false` and an `emptyDir` mounted at `/tmp`; no `hostNetwork`, `hostPID` or `hostIPC`, and no `hostPath` volume.
- Every resource sits in the PoC's namespace.
- A NetworkPolicy and a ResourceQuota are present.

Auth works through the ingress controller (forward auth in annotation mode): the controller calls your auth proxy before it forwards each request, so the proxy needs no route to the PoC pod. With ingress-nginx and an oauth2-proxy, for example:

```yaml
auth:
  ingress_annotations:
    nginx.ingress.kubernetes.io/auth-url: https://auth.poc.example.internal/oauth2/auth
    nginx.ingress.kubernetes.io/auth-signin: https://auth.poc.example.internal/oauth2/start?rd=$scheme://$host$request_uri
```

That is one example, not the expected controller. Another controller takes its own annotations; the NetworkPolicy still admits only the namespace in `network.ingress_controller_namespace`.

The default NetworkPolicy `app` selects the PoC's pod by its labels (not `{}`, so a cert-manager HTTP-01 solver pod in the namespace isn't cut off) and allows:

- in: from the namespace `network.ingress_controller_namespace` on `{port}`.
- out: to the namespace `network.dns_namespace` with the pods `network.dns_pod_labels` on 53, UDP and TCP; and to the egress proxy on `network.egress_proxy.port`, either `network.egress_proxy.namespace` with `pod_labels`, or `network.egress_proxy.cidr`.

Namespaces are matched by their `kubernetes.io/metadata.name` label. What the cluster needs: Kubernetes 1.22 or later, an ingress controller of the class the target names, the auth proxy it calls, and the Sealed Secrets controller.

#### PoC targets on Argo CD

Argo CD deploys every PoC. Agent smith commits a PoC's folder, then only reads Argo CD until the live Deployment matches that commit. It never calls Argo CD's sync, rollback or delete.

What Argo CD needs:

- Argo CD 2.8 or later. The ApplicationSet is created with `upsert=true`, which older versions don't take.
- A repository credential for the source repo. `argo.repo_url` is the URL that credential names, exactly.
- An AppProject (`argo.project`) that allows only what a PoC folder holds:
  - `sourceRepos` includes `repo_url`.
  - `destinations` allow the namespaces `poc-*` on `destination_server`.
  - `clusterResourceWhitelist` allows only the core `Namespace` kind (group `""`, kind `Namespace`).
  - `namespaceResourceWhitelist` allows `ResourceQuota`, `Deployment` (`apps`), `Service`, `ConfigMap`, `Ingress` (`networking.k8s.io`), `NetworkPolicy` (`networking.k8s.io`) and `SealedSecret` (`bitnami.com`).
  - A role whose policies allow `applications, get, <project>/*` and `applicationsets, get`, `create` and `update` on `<project>/*`.

For example:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: AppProject
metadata:
  name: pocs
  namespace: argocd
spec:
  sourceRepos:
    - https://git.example.internal/platform/poc-source.git
  destinations:
    - server: https://kubernetes.default.svc
      namespace: "poc-*"
  clusterResourceWhitelist:
    - group: ""
      kind: Namespace
  namespaceResourceWhitelist:
    - { group: "", kind: ResourceQuota }
    - { group: apps, kind: Deployment }
    - { group: "", kind: Service }
    - { group: "", kind: ConfigMap }
    - { group: networking.k8s.io, kind: Ingress }
    - { group: networking.k8s.io, kind: NetworkPolicy }
    - { group: bitnami.com, kind: SealedSecret }
  roles:
    - name: agent-smith
      policies:
        - p, proj:pocs:agent-smith, applications, get, pocs/*, allow
        - p, proj:pocs:agent-smith, applicationsets, get, pocs/*, allow
        - p, proj:pocs:agent-smith, applicationsets, create, pocs/*, allow
        - p, proj:pocs:agent-smith, applicationsets, update, pocs/*, allow
```

Mint the role's token and store it as an installation secret (the encrypted store), then name that secret in `argo.token_secret`:

```sh
argocd proj role create-token pocs agent-smith --expires-in 90d
```

A token created without `--expires-in` never expires. With it, the studio warns `token_warn_days` (14) before the expiry, and Test connection shows the date.

The ingress controller must publish Ingress status (`status.loadBalancer.ingress`). With ingress-nginx that is the `--publish-service` flag (`controller.publishService.enabled` in its Helm chart). Without it Argo CD keeps every Ingress Progressing and no PoC ever settles.

The target's Install upserts the ApplicationSet (`argo.application_set`, by default `pocs-<target name>`) in `argo.namespace`:

- a git directories generator over `<deploy_root>/*` at the source repo's default branch, with `requeueAfterSeconds` set to `argo.generator_requeue_seconds`;
- one Application per folder, named `poc-{{path.basename}}`, which is also its namespace;
- automated sync with prune and selfHeal, and the resources finalizer, so deleting a PoC's folder removes its namespace.

Two targets on one Argo CD with the same `argo.namespace` or `destination_server` are a blocking load finding, because their ApplicationSets would manage the same Applications.

## What the settings rail leaves out

`persistence:` is absent on purpose. It's bootstrap only, read from the file before the server can talk to a database, so making it editable in a UI backed by that database would be a circle. Change it in `agentsmith.yml` and restart.

`auth:` and `tool_runner:` are bootstrap as well: the token authority is wired before the database is reachable, and the scanners' runner is built at start. Both stay in the server's own `agentsmith.yml`. An import doesn't store them and says so, as it does for every other key of the file it doesn't keep.

`secrets:` is absent because it has its own catalog. The studio holds names, and values stay in the environment.
