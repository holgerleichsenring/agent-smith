# Phase 19 – Step 6: RBAC (ServiceAccount + Role + RoleBinding)

## Goal

Grant the Dispatcher pod the minimum Kubernetes permissions it needs to
spawn and monitor agent Jobs in the `agentsmith` namespace.
Cluster-wide permissions are NOT required — a namespace-scoped `Role` is sufficient.

---

## Files

```
k8s/base/rbac/
├── serviceaccount.yaml
├── role.yaml
└── rolebinding.yaml
```

---

## serviceaccount.yaml

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: agentsmith
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
```

The Dispatcher Deployment references this ServiceAccount via
`spec.template.spec.serviceAccountName: agentsmith`.

---

## role.yaml

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: agentsmith-job-manager
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
rules:
  - apiGroups: ["batch"]
    resources: ["jobs"]
    verbs: ["create", "delete", "get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods/log"]
    verbs: ["get"]
```

### Why these permissions?

| Permission | Reason |
|-----------|--------|
| `batch/jobs: create` | `JobSpawner.SpawnAsync()` calls `BatchV1.CreateNamespacedJobAsync()` |
| `batch/jobs: delete` | Future: allow cancelling a running job from Slack |
| `batch/jobs: get/list/watch` | K8s client needs these for internal resource version tracking |
| `pods: get/list/watch` | Read pod status to determine if job container is running |
| `pods/log: get` | Optional: surface agent container logs in Slack on error |

No access to Secrets, ConfigMaps, Deployments or any other resource type.

---

## rolebinding.yaml

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: agentsmith-job-manager
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
subjects:
  - kind: ServiceAccount
    name: agentsmith
    namespace: agentsmith
roleRef:
  kind: Role
  name: agentsmith-job-manager
  apiGroup: rbac.authorization.k8s.io
```

Binds the `agentsmith-job-manager` Role to the `agentsmith` ServiceAccount
in the same namespace.

---

## How the Dispatcher Uses the ServiceAccount

The K8s client in the Dispatcher resolves credentials automatically when
running inside a cluster:

```csharp
var k8sConfig = KubernetesClientConfiguration.IsInCluster()
    ? KubernetesClientConfiguration.InClusterConfig()   // uses mounted ServiceAccount token
    : KubernetesClientConfiguration.BuildConfigFromConfigFile(); // uses ~/.kube/config (local dev)
```

When running in-cluster, Kubernetes mounts a token at
`/var/run/secrets/kubernetes.io/serviceaccount/token` automatically.
The `KubernetesClient` library picks this up via `InClusterConfig()`.

---

## Kustomize Integration

All three files are listed in `k8s/base/kustomization.yaml`:

```yaml
resources:
  - rbac/serviceaccount.yaml
  - rbac/role.yaml
  - rbac/rolebinding.yaml
```

Kustomize automatically applies the `namespace: agentsmith` override
from the base `kustomization.yaml`, so the namespace field is set
consistently across all resources.

---

## Security Notes

- The Role is **namespace-scoped** (`Role`, not `ClusterRole`).
  The Dispatcher cannot touch resources in any other namespace.
- The agent Job pods themselves run with the **default** ServiceAccount,
  which has no special permissions — they only need outbound network access to:
  - Redis (in-cluster)
  - AI provider APIs (external)
  - Source control APIs (external)
- There is no `secrets: get` permission. Secrets are injected as environment
  variables by the Job spec — the Dispatcher never reads secret values directly.

---

## Verification

```bash
# Apply and verify
kubectl apply -k k8s/overlays/dev

# Check ServiceAccount exists
kubectl get serviceaccount agentsmith -n agentsmith

# Check Role exists
kubectl get role agentsmith-job-manager -n agentsmith -o yaml

# Check RoleBinding
kubectl get rolebinding agentsmith-job-manager -n agentsmith

# Test: can the dispatcher ServiceAccount create jobs?
kubectl auth can-i create jobs \
  --as=system:serviceaccount:agentsmith:agentsmith \
  -n agentsmith
# Expected: yes

# Test: can it access secrets? (should be no)
kubectl auth can-i get secrets \
  --as=system:serviceaccount:agentsmith:agentsmith \
  -n agentsmith
# Expected: no
```

---

## Definition of Done

- [ ] `serviceaccount.yaml` creates `agentsmith` ServiceAccount in `agentsmith` namespace
- [ ] `role.yaml` grants `batch/jobs` CRUD + `pods` read in `agentsmith` namespace
- [ ] `rolebinding.yaml` binds the Role to the ServiceAccount
- [ ] Dispatcher Deployment references `serviceAccountName: agentsmith`
- [ ] `kubectl auth can-i create jobs` returns `yes` for the ServiceAccount
- [ ] `kubectl auth can-i get secrets` returns `no` for the ServiceAccount
- [ ] `kubectl apply -k k8s/overlays/dev` applies all three RBAC resources cleanly