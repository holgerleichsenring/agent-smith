# Deployment templates

A code template says how a context is built. A deployment template says how it is delivered: the files an application of that kind needs to be pushed (a pipeline definition, a container build, Kubernetes manifests or the target platform's equivalent), the environments it goes to, and how a version moves from one environment to the next.

It lives where code templates live. A deployment template is a context of an ordinary agent-smith project, at a revision, and another project binds one of its contexts to it.

## Binding a context

In the studio it's the **templates** tab of the project drawer: add a template, set its kind to **deployment**, pick the template's project, repository and context, and add the environments. In YAML:

```yaml
projects:
  todolist:
    templates:
      - context: api
        kind: deployment
        project: platform-templates
        repo: delivery
        template_context: k8s-gitops
        revision: v1.0.0
        environments:
          - name: test
            repo: gitops-test
            approval: false
            parameters: { image: registry.example.com/todolist-api, replicas: "1" }
          - name: prod
            repo: gitops-prod
            parameters: { replicas: "3" }
```

| Key | What it says |
|---|---|
| `kind` | `deployment`. Absent, a binding is a code template |
| `environments[].name` | an environment the template declares |
| `environments[].repo` | a repo ref from the repos catalog holding the environment's manifest. Absent: the context's own repository. It is not one of the project's repos, so coding runs never check it out |
| `environments[].approval` | `true` (default): agent-smith opens the delivery's pull request and a person merges it. `false`: agent-smith merges it as well |
| `environments[].parameters` | values of the parameters the template declares, as strings |

Secrets are not configured here. The template names the secrets an application needs, and the files it delivers reference them by name. Where their values come from (a cluster secret store, the pipeline's variables) stays the platform's business.

## The template's declaration

A deployment template is a context of its repository with one more file beside its `context.yaml`: `.agentsmith/contexts/<template_context>/deployment.yaml`. The files it names are relative to the template repository's root.

```yaml
delivery: gitops_pr              # how a version reaches an environment: a pull request to its manifest
files:                           # what an application of this kind needs, copied or adapted into it
  - Dockerfile
  - .github/workflows/build-image.yml
  - deploy/
parameters:                      # values come from each environment's parameters in the binding
  - name: image
    description: the container image repository, without a tag
  - name: replicas
    default: "1"
secrets:                         # referenced by name in the files, never created or stored by agent-smith
  - name: DATABASE_URL
    description: the database connection string, from the cluster's secret store
environments:
  - name: test
    manifest: deploy/overlays/test/kustomization.yaml   # in the environment's repository
    version_key: images.0.newTag                        # the scalar that carries the version
  - name: prod
    manifest: deploy/overlays/prod/kustomization.yaml
    version_key: images.0.newTag
    promotes_from: test                                 # prod takes the version test runs
```

## Making a context deployable

Making a context deployable is ordinary work: a ticket, or a Work it out conversation, that says "make the api deployable to test". The run opens the deployment template read only, next to the code, as `template:deploy:<context>`. The coding master is told which environments this project binds and with which parameter values, and it copies or adapts the declared files into the context. A parameter with no value in the binding and no default in the template becomes a question to a person, with the value the master recommends; until it is answered the ticket waits in needs clarification. A secret is written into the files by its name only.

Because the template is opened like a code template, a coding run of a bound context needs to reach it: a revision that no longer exists fails the run before its first model call, the same as for a code template.

## Deploying

A deploy is a run like any other, with a run id, steps and a ledger. A person starts it from a ticket's page (**···** → **Deploy**) or with `POST /api/projects/{project}/deployments` (`runs.control`), naming the context, the environment and, for an entry environment, the version:

```json
{ "context": "api", "environment": "test", "version": "1.4.2", "ticketId": "42" }
```

The `deploy` run then:

1. finds the binding and the environment in the project's configuration, and reads the template's `deployment.yaml` at the bound revision
2. decides the version. An entry environment takes the version you named. An environment the template says `promotes_from` another takes the version that environment's manifest carries now, and a typed version for it is refused: the template decides what reaches it
3. reads the environment's manifest on its repository's default branch. If it already carries the version, nothing is written
4. otherwise writes the new version into the scalar at `version_key` (nothing else in the file changes) on a fresh branch `agent-smith/deploy/<context>-<environment>/<run id>`, and opens a pull request to the default branch
5. leaves that pull request for a person to merge when the environment needs approval (the default), or merges it in the project's merge mode (`merge.respect_branch_protection`) when approval is off. A merge the host only arms or refuses is reported, and the pull request waits for a person
6. reads the pull request's CI as the host reports it then (none, pending, green, or red with the failed checks and their links) and puts it in the run's outcome

A version is letters, digits, `.`, `_` and `-`, starting with a letter or digit, so a tag or a commit sha. A digest (`sha256:…`) is refused. agent-smith doesn't build images: your CI builds the image for a version, and the deploy points an environment at it.

The run is listed on the ticket it was started from, and it doesn't touch that ticket's own runs, status or lease. The pull request it opens isn't one of the ticket's pull requests, so the merge policy, base refreshes and rework never act on it.

## Delivering by a pipeline

A template can say `delivery: pipeline_trigger` instead. The deploy then starts the host's own pipeline for the environment and passes the version as one of its inputs:

```yaml
delivery: pipeline_trigger
parameters:
  - name: image
    default: registry.example.com/todolist-api
environments:
  - name: test
    pipeline: deploy.yml                  # GitHub: the workflow file or id. Azure DevOps: the pipeline's name (folder\name). GitLab: leave it out
    ref: main                             # optional: the branch or tag it runs on. Absent: the repository's default branch
    inputs:                               # GitHub workflow inputs, Azure DevOps template parameters, GitLab pipeline variables
      version: "{version}"
      environment: "{environment}"
      image: "{image}"
    manifest: deploy/overlays/test/kustomization.yaml   # optional: where the pipeline records the version it deployed
    version_key: images.0.newTag
  - name: prod
    pipeline: deploy.yml
    inputs: { version: "{version}", environment: "{environment}" }
    promotes_from: test
```

An input's value can use `{version}`, `{environment}`, `{context}` and the names of the template's parameters. A parameter takes the environment's value from the binding, else the template's default. Every environment has to pass `{version}` in one of its inputs, and a name that is none of these is refused when the template is read. A parameter with neither a value nor a default is refused when you deploy, so an input is never sent empty.

The pipeline is started in the environment's repository, or the context's own when the environment names none, with that repository's token:

| Host | What starts | Token needs |
|---|---|---|
| GitHub | `workflow_dispatch` of the workflow, which must declare `workflow_dispatch:` and every input you pass (GitHub answers 422 otherwise) | Actions: write (classic token: `repo`) |
| Azure DevOps | a run of the YAML pipeline with the inputs as template parameters, which the pipeline must declare (it answers 400 otherwise); a short `ref` is a branch | Build: read & execute |
| GitLab | a pipeline of the project for the ref, with the inputs as variables. A project that restricts pipeline variables refuses them (400/403) until a maintainer allows them | `api` |

The run's outcome names the started run with its link and what the host says of it then: running, green, or failed with the failed jobs (on Azure DevOps the failed tasks) and their links, read within `merge.ci_excerpt_chars` and `merge.ci_failed_checks_read`. A GitHub Enterprise Server that doesn't return the run it started gets the workflow's runs page instead; agent-smith doesn't guess which run is yours.

Every deploy starts the pipeline, a redeploy of the version an environment already runs too. A pipeline doesn't write a manifest agent-smith reads, so an environment promotes from a pipeline environment only when that one names a `manifest` and `version_key` its pipeline keeps up to date. `approval` means the same as for a pull request: only a person starts the deploy. Gate the run itself with the host's own environment approvals.

## Following a deploy

A deploy doesn't end with its run. agent-smith keeps reading what it delivered until it settles, and the deploy run's page and its line on the ticket show where it stands:

- a **pull request**: its state (open, merged, closed) and its CI on the head it has then, with each failed check's name, the end of its log and its link. It has settled once it is merged or closed and its CI is no longer running. While it waits for a person with its CI done, it is read every `tickets.pr_reread_interval_seconds`.
- a **pipeline run**: running, green, or failed with its failed jobs, their log ends and links. It has settled once it finished.

While CI or the pipeline runs, it is read every `merge.ci_poll_seconds`. One still running `merge.ci_pending_seconds` after delivery stops being followed and says so, with the link to look yourself. Excerpts are bounded by `merge.ci_excerpt_chars` and `merge.ci_failed_checks_read`. A deploy whose binding no longer leads to the repository it went to stops being followed with that reason. All of these are the project's existing merge and tickets settings in the studio; agent-smith only reads the host here.

Promoting automatically into environments without approval, and the series run calling a deploy, are planned.
