# Phase 19 – Step 7: Kustomize Base + Overlays

## Goal

Wire all K8s manifests together using Kustomize so the full stack can be deployed
with a single command in both local (dev) and production environments.

---

## Directory Structure

```
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── redis/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── dispatcher/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── rbac/
│   │   ├── serviceaccount.yaml
│   │   ├── role.yaml
│   │   └── rolebinding.yaml
│   └── configmap/
│       └── agentsmith-config.yaml
├── overlays/
│   ├── dev/
│   │   ├── kustomization.yaml
│   │   ├── patch-dispatcher-dev.yaml
│   │   └── patch-service-nodeport.yaml   ← NodePort for local browser access
│   └── prod/
│       ├── kustomization.yaml
│       ├── patch-dispatcher-prod.yaml
│       └── ingress.yaml                  ← Ingress + TLS for production
└── secret-template.yaml                  ← template only, never committed
```

---

## Base Kustomization

**`k8s/base/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

resources:
  - namespace.yaml
  - redis/deployment.yaml
  - redis/service.yaml
  - dispatcher/deployment.yaml
  - dispatcher/service.yaml
  - rbac/serviceaccount.yaml
  - rbac/role.yaml
  - rbac/rolebinding.yaml
  - configmap/agentsmith-config.yaml
```

The base is environment-agnostic. No image tags, no pull policies, no replica
counts are environment-specific — those are all patched in overlays.

---

## Dev Overlay

**`k8s/overlays/dev/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

bases:
  - ../../base

patches:
  - path: patch-dispatcher-dev.yaml
    target:
      kind: Deployment
      name: agentsmith-dispatcher
  - path: patch-service-nodeport.yaml
    target:
      kind: Service
      name: agentsmith-dispatcher
```

**`k8s/overlays/dev/patch-dispatcher-dev.yaml`**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  replicas: 1
  template:
    spec:
      containers:
        - name: dispatcher
          imagePullPolicy: Never      # uses locally built image (Docker Desktop / kind)
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Development"
            - name: IMAGE_PULL_POLICY
              value: "Never"          # passed to JobSpawner for agent containers
```

**`k8s/overlays/dev/patch-service-nodeport.yaml`**

Exposes the dispatcher on a static local port so `curl localhost:30080/health`
works without running `kubectl port-forward`.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  type: NodePort
  ports:
    - name: http
      port: 80
      targetPort: 8080
      nodePort: 30080
      protocol: TCP
```

---

## Prod Overlay

**`k8s/overlays/prod/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

bases:
  - ../../base

resources:
  - ingress.yaml

patches:
  - path: patch-dispatcher-prod.yaml
    target:
      kind: Deployment
      name: agentsmith-dispatcher
```

**`k8s/overlays/prod/patch-dispatcher-prod.yaml`**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  replicas: 2
  template:
    spec:
      containers:
        - name: dispatcher
          imagePullPolicy: Always
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: IMAGE_PULL_POLICY
              value: "Always"
          resources:
            requests:
              cpu: "200m"
              memory: "384Mi"
            limits:
              cpu: "1000m"
              memory: "768Mi"
```

**`k8s/overlays/prod/ingress.yaml`**

Replace `agentsmith.yourdomain.com` with your actual hostname.
Requires an Ingress controller (e.g. nginx-ingress) and cert-manager for TLS.

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  annotations:
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - agentsmith.yourdomain.com
      secretName: agentsmith-tls
  rules:
    - host: agentsmith.yourdomain.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: agentsmith-dispatcher
                port:
                  name: http
```

---

## Deployment Commands

### Dev (Docker Desktop / kind)

```bash
# Build images locally first
docker build -t agentsmith:latest .
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .

# Create namespace + all resources
kubectl apply -k k8s/overlays/dev

# Check status
kubectl get pods -n agentsmith
kubectl get svc -n agentsmith

# Access dispatcher directly (NodePort)
curl http://localhost:30080/health

# Or use port-forward as alternative
kubectl port-forward svc/agentsmith-dispatcher 8080:80 -n agentsmith
```

### Prod

```bash
# Tag and push images to your registry
docker build -t registry.yourdomain.com/agentsmith:v1.0.0 .
docker build -f Dockerfile.dispatcher -t registry.yourdomain.com/agentsmith-dispatcher:v1.0.0 .
docker push registry.yourdomain.com/agentsmith:v1.0.0
docker push registry.yourdomain.com/agentsmith-dispatcher:v1.0.0

# Update image references in prod overlay (or use kustomize edit set image)
kustomize edit set image agentsmith=registry.yourdomain.com/agentsmith:v1.0.0

# Apply
kubectl apply -k k8s/overlays/prod

# Check rollout
kubectl rollout status deployment/agentsmith-dispatcher -n agentsmith
```

---

## Secret Management

Secrets are **never** included in Kustomize overlays. Always apply them separately:

```bash
# From template (fill in values first)
cp k8s/secret-template.yaml k8s/secret-local.yaml
# Edit k8s/secret-local.yaml
kubectl apply -f k8s/secret-local.yaml
```

`k8s/secret-local.yaml` is in `.gitignore` and must never be committed.

For production, use one of:
- **Sealed Secrets**: `kubeseal` encrypts the secret for safe git storage
- **External Secrets Operator**: pulls secrets from AWS Secrets Manager / Vault / GCP SM
- **HashiCorp Vault**: agent sidecar injects secrets at pod startup

---

## ConfigMap from Local Config

The `agentsmith-config` ConfigMap is checked in with a placeholder template.
In practice, regenerate it from your local config before deploying:

```bash
kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml=config/agentsmith.yml \
  -n agentsmith \
  --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
```

---

## Definition of Done

- [ ] `kubectl apply -k k8s/overlays/dev` succeeds without errors
- [ ] `kubectl get pods -n agentsmith` shows redis + dispatcher Running
- [ ] `curl http://localhost:30080/health` returns `{"status":"ok"}`
- [ ] Dev overlay uses `imagePullPolicy: Never`
- [ ] Prod overlay uses `imagePullPolicy: Always`, 2 replicas, Ingress
- [ ] No secrets are embedded in any Kustomize file
- [ ] `kubectl apply -k k8s/overlays/prod` renders valid YAML (dry-run)