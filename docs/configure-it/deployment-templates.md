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
