# Phase 19 – Step 2: Redis Deployment + Service

## Goal

Deploy Redis as an ephemeral in-memory message bus inside the `agentsmith` namespace.
No PersistentVolume — messages are short-lived (job lifetime = minutes).
If Redis restarts, in-flight jobs are considered lost (acceptable tradeoff).

---

## Files

- `k8s/base/redis/deployment.yaml`
- `k8s/base/redis/service.yaml`

---

## Redis Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: agentsmith
  labels:
    app: redis
    app.kubernetes.io/name: redis
    app.kubernetes.io/component: message-bus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          args:
            - "--maxmemory"
            - "256mb"
            - "--maxmemory-policy"
            - "allkeys-lru"
            - "--save"
            - ""        # disable RDB persistence entirely
          ports:
            - name: redis
              containerPort: 6379
              protocol: TCP
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "500m"
              memory: "384Mi"
          livenessProbe:
            exec:
              command: ["redis-cli", "ping"]
            initialDelaySeconds: 10
            periodSeconds: 15
            timeoutSeconds: 3
          readinessProbe:
            exec:
              command: ["redis-cli", "ping"]
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 3
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: false
            runAsNonRoot: true
            runAsUser: 999   # redis user in the official image
```

---

## Redis Service

ClusterIP service — Redis is internal only. Never exposed outside the cluster.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: redis
  namespace: agentsmith
  labels:
    app: redis
    app.kubernetes.io/name: redis
    app.kubernetes.io/component: message-bus
spec:
  type: ClusterIP
  selector:
    app: redis
  ports:
    - name: redis
      port: 6379
      targetPort: 6379
      protocol: TCP
```

The in-cluster DNS name is: `redis.agentsmith.svc.cluster.local:6379`
Short form (same namespace): `redis:6379`

The `redis-url` secret value should be: `redis://redis:6379`

---

## Design Decisions

### No PersistentVolume

Redis runs in pure in-memory mode (`--save ""`). Reasons:
- All messages are ephemeral — job lifetime is measured in minutes
- Stream TTL is 2 hours; streams are explicitly deleted on job completion
- Adding a PV later requires only one config change (`--appendonly yes` + PVC)
- Simpler deployment, no storage class dependency

### maxmemory-policy: allkeys-lru

If memory pressure occurs (unlikely at 256mb for small workloads),
LRU eviction removes the least-recently-used keys. This means old stream
entries are evicted before active ones.

### Single Replica

Redis does not support multi-replica writes without Redis Cluster or Sentinel.
For this workload (short-lived jobs, local channels) a single replica is correct.
HA upgrade path: Redis Cluster or an external managed Redis (Upstash, ElastiCache).

---

## Connection String

The Dispatcher and agent containers connect using:

```
REDIS_URL=redis://redis:6379
```

Stored in the `agentsmith-secrets` K8s Secret under key `redis-url`.

---

## Registering in Kustomize Base

Both files are listed in `k8s/base/kustomization.yaml`:

```yaml
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

---

## Verification

```bash
# Apply base manifests
kubectl apply -k k8s/overlays/dev

# Check Redis pod is Running
kubectl get pods -n agentsmith -l app=redis

# Verify Redis is reachable from inside the cluster
kubectl run redis-test --rm -it --image=redis:7-alpine -n agentsmith -- \
  redis-cli -h redis ping
# Expected output: PONG
```

---

## Definition of Done

- [ ] `redis/deployment.yaml` applies without errors
- [ ] `redis/service.yaml` applies without errors
- [ ] Redis pod reaches `Running` state
- [ ] `redis-cli ping` returns `PONG` from inside the cluster
- [ ] No PersistentVolume or PersistentVolumeClaim referenced
- [ ] `--save ""` disables persistence
- [ ] Memory limit set to 384Mi (headroom above 256mb maxmemory)