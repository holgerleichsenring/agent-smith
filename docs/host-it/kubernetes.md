# Host it: Kubernetes

For shared use and production. Multi-replica orchestrator, Redis as a stateful service, sandbox pods created on demand and disposed when each run ends. No CRDs, no operators — stock Kubernetes objects.

## What this gets you

- Multiple orchestrator replicas. One holds the polling leader lease, all of them serve webhooks (any can accept; the work goes into Redis).
- Sandbox pods created per repo per run, then deleted. The orchestrator's `ServiceAccount` has permission to create / delete pods in its own namespace.
- Redis as a StatefulSet with persistence (or use a managed Redis if you prefer; pass the URL via `REDIS_URL`).
- Survives node failures because Kubernetes reschedules everything.

## The manifests

Apply everything below into a namespace `agent-smith`. Adjust resource limits to your cluster. Examples use the `TodoList` config.

### Namespace + ServiceAccount + RBAC

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: agent-smith
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: agent-smith-orchestrator
  namespace: agent-smith
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: agent-smith-sandbox-manager
  namespace: agent-smith
rules:
  - apiGroups: [""]
    resources: ["pods", "pods/log", "pods/exec"]
    verbs: ["get", "list", "watch", "create", "delete"]
  - apiGroups: [""]
    resources: ["configmaps", "secrets"]
    verbs: ["get", "list"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: agent-smith-sandbox-manager
  namespace: agent-smith
subjects:
  - kind: ServiceAccount
    name: agent-smith-orchestrator
    namespace: agent-smith
roleRef:
  kind: Role
  name: agent-smith-sandbox-manager
  apiGroup: rbac.authorization.k8s.io
```

The orchestrator needs `pods/exec` because the sandbox-agent injection runs an `kubectl exec`-style init step (in cluster, via the Kubernetes API).

### Secret

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: agent-smith-secrets
  namespace: agent-smith
type: Opaque
stringData:
  AZURE_OPENAI_API_KEY: "..."
  AZURE_DEVOPS_TOKEN:   "..."
  AZURE_DEVOPS_WEBHOOK_SECRET: "..."
```

For real production: use a secrets operator (Vault, External Secrets, Sealed Secrets). Don't keep raw keys in version-controlled YAML.

### ConfigMap with `agentsmith.yml`

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agent-smith-config
  namespace: agent-smith
data:
  agentsmith.yml: |
    # The full TodoList Azure DevOps example —
    # see docs/connect-your-stuff/tracker-azure-devops.md
    sandbox:
      agent_registry: holgerleichsenring
      agent_version: 0.60.1

    orchestrator:
      registry: holgerleichsenring
      version: 0.60.1

    agents:
      azure-openai-default:
        type: azure_openai
        endpoint: https://oai-acme-dev.openai.azure.com
        # ... full agent block ...

    repos:
      todolist-api:
        type: azure_devops
        url: https://dev.azure.com/acme-org/Platform/_git/TodoList.Api
        auth: azure_devops_token
      # ... rest of the TodoList repos ...

    trackers:
      acme-platform:
        type: azure_devops
        url: https://dev.azure.com/acme-org
        organization: acme-org
        project: Platform
        auth: azure_devops_token
        webhook_secret: ${AZURE_DEVOPS_WEBHOOK_SECRET}
        open_states: [New, Active]
        done_status: Resolved

    projects:
      azuredevops-todolist:
        agent: azure-openai-default
        tracker: acme-platform
        repos: [todolist-api, todolist-worker, todolist-web, todolist-docs]
        # ... trigger block ...

    skills:
      source: default
      version: v3.0.1
      cache_dir: /var/lib/agentsmith/skills

    secrets:
      azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
      azure_devops_token:   ${AZURE_DEVOPS_TOKEN}
```

### Redis (StatefulSet)

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: agent-smith-redis
  namespace: agent-smith
spec:
  serviceName: agent-smith-redis
  replicas: 1
  selector:
    matchLabels: { app: agent-smith-redis }
  template:
    metadata:
      labels: { app: agent-smith-redis }
    spec:
      containers:
        - name: redis
          image: redis:7
          args: ["redis-server", "--appendonly", "yes"]
          ports:
            - containerPort: 6379
          volumeMounts:
            - name: data
              mountPath: /data
  volumeClaimTemplates:
    - metadata: { name: data }
      spec:
        accessModes: [ReadWriteOnce]
        resources: { requests: { storage: 5Gi } }
---
apiVersion: v1
kind: Service
metadata:
  name: agent-smith-redis
  namespace: agent-smith
spec:
  selector: { app: agent-smith-redis }
  ports:
    - port: 6379
      targetPort: 6379
  clusterIP: None
```

### Orchestrator (Deployment + Service)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agent-smith-orchestrator
  namespace: agent-smith
spec:
  replicas: 2
  selector:
    matchLabels: { app: agent-smith-orchestrator }
  template:
    metadata:
      labels: { app: agent-smith-orchestrator }
    spec:
      serviceAccountName: agent-smith-orchestrator
      containers:
        - name: orchestrator
          image: holgerleichsenring/agent-smith:0.60.1
          imagePullPolicy: IfNotPresent
          env:
            - name: AGENTSMITH_CONFIG
              value: /etc/agent-smith/agentsmith.yml
            - name: REDIS_URL
              value: redis://agent-smith-redis:6379
            - name: SANDBOX_TYPE
              value: kubernetes
            - name: KUBERNETES_NAMESPACE
              valueFrom: { fieldRef: { fieldPath: metadata.namespace } }
          envFrom:
            - secretRef: { name: agent-smith-secrets }
          ports:
            - name: http
              containerPort: 8080
          volumeMounts:
            - name: config
              mountPath: /etc/agent-smith
              readOnly: true
            - name: runs
              mountPath: /var/lib/agent-smith/runs
            - name: skills
              mountPath: /var/lib/agentsmith/skills
          resources:
            requests: { cpu: 200m, memory: 512Mi }
            limits:   { cpu: 2,    memory: 2Gi }
      volumes:
        - name: config
          configMap: { name: agent-smith-config }
        - name: runs
          persistentVolumeClaim: { claimName: agent-smith-runs }
        - name: skills
          emptyDir: {}                # rebuilt from agent-smith-skills repo at startup
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: agent-smith-runs
  namespace: agent-smith
spec:
  accessModes: [ReadWriteMany]        # multi-replica needs RWX; if you only have RWO, set replicas: 1
  resources: { requests: { storage: 20Gi } }
---
apiVersion: v1
kind: Service
metadata:
  name: agent-smith-orchestrator
  namespace: agent-smith
spec:
  selector: { app: agent-smith-orchestrator }
  ports:
    - name: http
      port: 80
      targetPort: 8080
  type: ClusterIP
```

### Ingress

Whatever ingress your cluster uses. NGINX example:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: agent-smith
  namespace: agent-smith
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  ingressClassName: nginx
  tls:
    - hosts: [agent-smith.your-domain.example]
      secretName: agent-smith-tls
  rules:
    - host: agent-smith.your-domain.example
      http:
        paths:
          - path: /webhooks
            pathType: Prefix
            backend:
              service:
                name: agent-smith-orchestrator
                port: { number: 80 }
```

Point your tracker webhooks at `https://agent-smith.your-domain.example/webhooks/{tracker-type}`.

## Sandbox pods

The orchestrator creates a pod per repo per run, named `sandbox-{run-id}-{repo-name}`. Each pod has an init-container that copies the sandbox-agent binary into a shared `emptyDir`, then the main toolchain container starts and the agent binary takes over the entrypoint.

You don't need to pre-create sandbox pods. The orchestrator creates them on demand using the `SandboxSpecBuilder` rules — toolchain image per repo, resources from the `agentsmith.yml` `sandbox` block, namespace = `KUBERNETES_NAMESPACE`.

When a run finishes (success or failure), `DisposeAsync` deletes the pods. Belt-and-suspenders: the pods are also owned by the orchestrator pod via `ownerReferences`, so if the orchestrator dies the sandboxes get garbage-collected.

## Updating

```yaml
# Bump both numbers together
agent_version: 0.60.2
version: 0.60.1   # ← was; bumping to 0.60.2

# In the orchestrator Deployment image:
image: holgerleichsenring/agent-smith:0.60.2
```

```bash
kubectl apply -f agent-smith-deployment.yaml
kubectl rollout status deployment/agent-smith-orchestrator -n agent-smith
```

`RollingUpdate` is the default strategy; one replica at a time, no downtime. In-flight runs continue under the old orchestrator until it drains.

## Resources

Defaults in the example: 200m CPU / 512Mi memory request, 2 CPU / 2Gi limit per replica. The orchestrator's CPU usage spikes during plan / review / verify rounds (waiting on LLM calls is cheap; processing the structured observations isn't free). Memory is mostly the in-flight conversation context — 2Gi handles every run I've seen.

The sandbox pods are sized per-run from the `sandbox.resources` block in `agentsmith.yml` (defaults to 100m / 256Mi requests, 1 CPU / 1Gi limits per sandbox).

## Next

- [Webhooks](../trigger-it/webhooks.md) — point them at the ingress URL.
- [docker-compose](docker-compose.md) — the simpler version for one host.
- [Methodology](../how-it-works/methodology.md) — what's actually running in those sandbox pods.
