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
