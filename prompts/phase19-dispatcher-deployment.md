# Phase 19 – Step 5: Dispatcher Deployment + Service

## Goal

Deploy the `AgentSmith.Dispatcher` as a long-running Kubernetes `Deployment`
with a `ClusterIP` Service, liveness/readiness probes, and full secret injection.

---

## Files

### `k8s/base/dispatcher/deployment.yaml`

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  labels:
    app: agentsmith-dispatcher
    app.kubernetes.io/name: agentsmith-dispatcher
    app.kubernetes.io/component: dispatcher
spec:
  replicas: 1
  selector:
    matchLabels:
      app: agentsmith-dispatcher
  template:
    metadata:
      labels:
        app: agentsmith-dispatcher
    spec:
      serviceAccountName: agentsmith
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        runAsGroup: 1000
        fsGroup: 1000
      containers:
        - name: dispatcher
          image: agentsmith-dispatcher:latest
          imagePullPolicy: IfNotPresent
          ports:
            - name: http
              containerPort: 8080
              protocol: TCP
          env:
            - name: ASPNETCORE_URLS
              value: "http://+:8080"
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: REDIS_URL
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: redis-url
            - name: SLACK_BOT_TOKEN
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-bot-token
            - name: SLACK_SIGNING_SECRET
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-signing-secret
            - name: K8S_NAMESPACE
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
            - name: AGENTSMITH_IMAGE
              value: "agentsmith:latest"
            - name: IMAGE_PULL_POLICY
              value: "IfNotPresent"
            - name: K8S_SECRET_NAME
              value: "agentsmith-secrets"
          volumeMounts:
            - name: config
              mountPath: /app/config
              readOnly: true
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 3
          resources:
            requests:
              cpu: "100m"
              memory: "256Mi"
            limits:
              cpu: "500m"
              memory: "512Mi"
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: false
      volumes:
        - name: config
          configMap:
            name: agentsmith-config
      restartPolicy: Always
```

---

### `k8s/base/dispatcher/service.yaml`

```yaml
apiVersion: v1
kind: Service
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  labels:
    app: agentsmith-dispatcher
    app.kubernetes.io/name: agentsmith-dispatcher
    app.kubernetes.io/component: dispatcher
spec:
  type: ClusterIP
  selector:
    app: agentsmith-dispatcher
  ports:
    - name: http
      port: 80
      targetPort: 8080
      protocol: TCP
```

The base service is `ClusterIP` only. External access is handled by overlay-specific
patches (NodePort for dev, Ingress for prod).

---

## Design Decisions

### One Dispatcher Replica (base)
The base manifest sets `replicas: 1`. The prod overlay scales to 2.
The dispatcher is stateless (all state lives in Redis), so scaling is safe.

### ServiceAccount
`serviceAccountName: agentsmith` — the same service account used by the RBAC
Role+RoleBinding that grants Job creation rights. Defined in `rbac/serviceaccount.yaml`.

### Config Volume
`agentsmith.yml` is mounted from the `agentsmith-config` ConfigMap.
The Dispatcher reads it at runtime via `IConfigurationLoader` for the list/create fast-path.
Changing the config requires updating the ConfigMap and restarting the pod.

### Secret Injection
All credentials come from `agentsmith-secrets` via `secretKeyRef`.
The pod never has access to the raw secret file, only the specific keys it needs.

### Health Probes
- **Readiness**: begins checking after 5s; pod receives traffic only when `/health` returns 200
- **Liveness**: begins checking after 15s; restarts pod if 3 consecutive checks fail
- Both use `GET /health` which is implemented in `Program.cs` and returns immediately

### Resource Limits
Base limits are conservative (500m CPU / 512Mi RAM). The dispatcher is mostly
I/O bound (Redis + Slack API calls). The prod overlay increases these for 2-replica headroom.

### `readOnlyRootFilesystem: false`
Required because ASP.NET Core writes temp files to `/tmp`. An alternative is to mount
an emptyDir at `/tmp`, but `false` is simpler and acceptable for this workload.

---

## Environment Variables Summary

| Variable | Source | Purpose |
|----------|--------|---------|
| `ASPNETCORE_URLS` | hardcoded | Bind to port 8080 |
| `ASPNETCORE_ENVIRONMENT` | hardcoded | `Production` (overridden in dev overlay) |
| `REDIS_URL` | secret | Redis connection string |
| `SLACK_BOT_TOKEN` | secret | Slack Web API token |
| `SLACK_SIGNING_SECRET` | secret | Slack request verification |
| `K8S_NAMESPACE` | fieldRef | Namespace for spawning jobs (auto-detected) |
| `AGENTSMITH_IMAGE` | hardcoded | Image for spawned K8s Jobs |
| `IMAGE_PULL_POLICY` | hardcoded | Pull policy for spawned jobs |
| `K8S_SECRET_NAME` | hardcoded | Secret name injected into spawned jobs |

---

## Definition of Done

- [ ] `deployment.yaml` applies cleanly to the cluster
- [ ] `service.yaml` creates a ClusterIP service on port 80 → 8080
- [ ] Pod starts with non-root UID 1000
- [ ] Config volume is mounted at `/app/config`
- [ ] Liveness and readiness probes pass within 30s of pod start
- [ ] `kubectl port-forward svc/agentsmith-dispatcher 8080:80 -n agentsmith`
      then `curl http://localhost:8080/health` returns `{"status":"ok",...}`
