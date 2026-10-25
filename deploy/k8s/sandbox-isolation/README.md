# Sandbox isolation (opt-in)

`kubectl apply -f deploy/k8s/` does not touch this folder. Without it, sandbox pods run in the
`agentsmith` namespace next to the server, exactly as they always have.

With it, sandbox pods move to a namespace of their own, `agentsmith-sandboxes`, and a
NetworkPolicy limits what they can reach:

| File | What it creates |
|---|---|
| `1-namespace-sandboxes.yaml` | The namespace `agentsmith-sandboxes`. |
| `2-rbac-sandboxes.yaml` | Role and RoleBinding `agentsmith-sandbox-manager`: the server's ServiceAccount may create, watch and delete pods and read the ResourceQuota there. |
| `3-networkpolicy-sandbox.yaml` | NetworkPolicy `agentsmith-sandbox`: no inbound connections; outbound only cluster DNS, Redis on 6379, and ports 80 and 443 on public addresses. |

What you get: a sandbox can only use Secrets created in `agentsmith-sandboxes`, so it cannot
mount the server's `agentsmith-secrets`. It cannot reach the server, the dashboard, other
sandboxes or the cloud metadata service.

This is separate from pod hardening. Hardening (uid 1000, no capabilities, no service-account
token) is the installation setting `sandbox.hardening_enabled`, off by default: **Harden sandboxes**
under **Configuration → Settings → Sandbox**, applied after a server restart. You can turn on
either one alone.

## Before you turn it on

Sandbox pods look for everything in their own namespace. Create these in `agentsmith-sandboxes`
first, or runs fail once the server moves:

1. **Sandbox Secrets.** Every Secret a project names under `sandbox.secrets` (`secretName:key`).
   A Secret that exists only in `agentsmith` no longer resolves and the pod does not start.
2. **Image pull secrets.** Every name in `sandbox.image_pull_secrets`.
3. **ResourceQuota.** The capacity check reads the quota of the namespace sandboxes run in.
   Move or copy your quota to `agentsmith-sandboxes`, or runs are no longer queued by capacity.
4. **Network allowances.** Read the ALLOWANCES section at the top of
   `3-networkpolicy-sandbox.yaml`. A project that clones from a private git host, restores from
   a private feed, scans a private target or needs a port other than 80 and 443 fails until its
   address is allowed there. The same section covers NodeLocal DNS and OpenShift DNS.
5. **The `sandbox-redis-url` key** in `agentsmith-secrets`. A pod in another namespace cannot
   resolve the short name `redis`, so it needs the full name, normally
   `redis.agentsmith.svc.cluster.local:6379`. `deploy/apply-k8s-secret.sh` writes this key;
   `4-secret-template.yaml` has it too.

The NetworkPolicy only has an effect if your cluster's network plugin enforces NetworkPolicy.

## Turning it on

Apply the folder:

```bash
kubectl apply -f deploy/k8s/sandbox-isolation/
```

Then add these two entries to the `server` container's `env` in
`deploy/k8s/8-deployment-server.yaml` (or your own server Deployment), and apply it:

```yaml
            - name: K8S_SANDBOX_NAMESPACE
              value: "agentsmith-sandboxes"
            - name: SANDBOX_REDIS_URL
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: sandbox-redis-url
```

Set both or neither. If the namespace is set and `SANDBOX_REDIS_URL` is not, the server reports
a blocking startup finding and starts no sandbox.

Keep the env entries in your copy of the Deployment when you update. The shipped
`8-deployment-server.yaml` does not carry them.

## Checking it works

```bash
kubectl -n agentsmith rollout status deployment/agentsmith-server
curl -s https://<your-host>/health      # no blocking SANDBOX_REDIS_URL finding
kubectl -n agentsmith-sandboxes get pods -l app=agentsmith-sandbox -w
```

Start a run. Its sandbox pods should appear in `agentsmith-sandboxes`, and the run should clone,
restore, build and push as before. From a sandbox pod, the server service and
`169.254.169.254` should not answer.

## Turning it off

Remove the two env entries and apply the server Deployment. Sandboxes go back to `agentsmith`
on the next run. You can then delete the namespace with
`kubectl delete -f deploy/k8s/sandbox-isolation/`.

## Egress proxy

`deploy/k8s/examples/sandbox-egress-proxy.yaml` builds on this folder. It replaces the
NetworkPolicy here with one that allows only DNS, Redis and a squid proxy with a domain
allowlist. Apply it after this folder, and again after each apply of this folder.
