# Phase 19 – Step 4: ConfigMap for agentsmith.yml

## Goal

Mount the `agentsmith.yml` configuration into the Dispatcher container via a
Kubernetes ConfigMap. This avoids baking environment-specific config into the
container image and allows config changes without rebuilding.

---

## File

`k8s/base/configmap/agentsmith-config.yaml`

---

## Manifest

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agentsmith-config
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: config
data:
  agentsmith.yml: |
    # This ConfigMap is generated from config/agentsmith.yml.
    # Do not edit manually. Regenerate with:
    #
    #   kubectl create configmap agentsmith-config \
    #     --from-file=agentsmith.yml=config/agentsmith.yml \
    #     -n agentsmith \
    #     --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
    #
    projects:
      my-project:
        source:
          type: GitHub
          url: https://github.com/yourorg/your-repo
          auth: token
        tickets:
          type: GitHub
          url: https://github.com/yourorg/your-repo
          auth: token
        agent:
          type: Claude
          model: claude-sonnet-4-20250514
          retry:
            max_retries: 5
            initial_delay_ms: 2000
            backoff_multiplier: 2.0
            max_delay_ms: 60000
          cache:
            enabled: true
            strategy: automatic
          compaction:
            enabled: true
            threshold_iterations: 8
            max_context_tokens: 80000
            keep_recent_iterations: 3
            summary_model: claude-haiku-4-5-20251001
          models:
            scout:
              model: claude-haiku-4-5-20251001
              max_tokens: 4096
            primary:
              model: claude-sonnet-4-20250514
              max_tokens: 8192
        pipeline: fix-bug
        coding_principles_path: ./config/coding-principles.md

    pipelines:
      fix-bug:
        commands:
          - FetchTicketCommand
          - CheckoutSourceCommand
          - LoadCodingPrinciplesCommand
          - AnalyzeCodeCommand
          - GeneratePlanCommand
          - ApprovalCommand
          - AgenticExecuteCommand
          - TestCommand
          - CommitAndPRCommand

    secrets:
      github_token: ${GITHUB_TOKEN}
      anthropic_api_key: ${ANTHROPIC_API_KEY}
      azure_devops_token: ${AZURE_DEVOPS_TOKEN}
      openai_api_key: ${OPENAI_API_KEY}
      gemini_api_key: ${GEMINI_API_KEY}
```

---

## How It Is Mounted

The Dispatcher `Deployment` mounts this ConfigMap as a volume at `/app/config`:

```yaml
volumeMounts:
  - name: config
    mountPath: /app/config
    readOnly: true

volumes:
  - name: config
    configMap:
      name: agentsmith-config
```

The Dispatcher reads `config/agentsmith.yml` via `IConfigurationLoader` at startup
and on every `list tickets` / `create ticket` request.

---

## Regenerating from Local Config

When you change your local `config/agentsmith.yml`, regenerate the ConfigMap with:

```bash
kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml=config/agentsmith.yml \
  -n agentsmith \
  --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
```

Then apply with:

```bash
kubectl apply -k k8s/overlays/dev   # or prod
```

The Dispatcher pod must be restarted to pick up config changes
(ConfigMap updates are not hot-reloaded):

```bash
kubectl rollout restart deployment/agentsmith-dispatcher -n agentsmith
```

---

## Design Notes

- **Never bake config into the image.** Project URLs, token types, pipeline definitions
  and model choices all belong in the ConfigMap, not in the Dockerfile.
- **Secrets stay in the K8s Secret.** The ConfigMap holds only non-sensitive structure.
  All `${ENV_VAR}` references in `agentsmith.yml` are resolved at runtime from
  environment variables injected via the `agentsmith-secrets` Secret.
- **One ConfigMap per environment.** The dev and prod overlays can each generate their
  own ConfigMap from different source files if needed.
- **Placeholder content in base.** The `k8s/base/configmap/agentsmith-config.yaml`
  contains a working template. Real projects override it by regenerating from their
  own `config/agentsmith.yml`.

---

## Definition of Done

- [ ] `k8s/base/configmap/agentsmith-config.yaml` exists and is valid YAML
- [ ] ConfigMap is referenced in `k8s/base/kustomization.yaml`
- [ ] Dispatcher Deployment mounts it at `/app/config`
- [ ] `kubectl apply -k k8s/overlays/dev` applies the ConfigMap without errors
- [ ] Dispatcher can read `config/agentsmith.yml` inside the pod