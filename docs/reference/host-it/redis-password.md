# Requiring a Redis password

The reference Redis (`deploy/k8s/6-deployment-redis.yaml`, the `redis` service in
`deploy/docker-compose.example.yml`) starts without a password. Anything that can reach
port 6379 can read the queue, the event streams and the steps the server sends to its
sandboxes. You can require a password today without a code change: the server and the
sandbox agent both parse `REDIS_URL` with the same library, which honours
`host:port,password=…`.

`REDIS_URL` stays in the `host:port` form, not a URI:

```
REDIS_URL=redis:6379,password=<the password>
```

## Kubernetes

1. Add the password to the Secret under its own key, next to `redis-url`:

   ```yaml
   stringData:
     redis-password: "<a long random value>"
     redis-url: "redis:6379,password=<the same value>"
   ```

2. Give the Redis container the password from that key with `optional: false`, and start
   it through a guard that refuses an empty value:

   ```yaml
   env:
     - name: REDIS_PASSWORD
       valueFrom:
         secretKeyRef:
           name: agentsmith-secrets
           key: redis-password
           optional: false
   command: ["sh", "-c"]
   args:
     - test -n "$REDIS_PASSWORD" && exec redis-server --requirepass "$REDIS_PASSWORD"
       --maxmemory 256mb --maxmemory-policy allkeys-lru --save ""
   ```

3. Let the liveness and readiness probes authenticate. `redis-cli` reads the password
   from `REDISCLI_AUTH`, so the probe command itself stays the same and the container's
   environment carries the variable:

   ```yaml
   - name: REDISCLI_AUTH
     valueFrom:
       secretKeyRef:
         name: agentsmith-secrets
         key: redis-password
         optional: false
   ```

## Docker Compose

Put the password in `../.env` as `REDIS_PASSWORD=…`, then in the compose file:

- the `redis` service runs
  `sh -c 'test -n "$REDIS_PASSWORD" && exec redis-server --requirepass "$REDIS_PASSWORD" --maxmemory 256mb --maxmemory-policy allkeys-lru --save "" --appendonly yes'`
  with `REDIS_PASSWORD` and `REDISCLI_AUTH` set from the same value;
- its healthcheck `redis-cli ping` then authenticates through `REDISCLI_AUTH`;
- the server's literal `REDIS_URL=redis:6379` becomes
  `REDIS_URL=redis:6379,password=${REDIS_PASSWORD}`.

## Two ways this fails open, and what catches each

- **A missing key.** With `optional: true` a missing key leaves the variable unset, and
  a composed value such as `$(REDIS_PASSWORD)` stays literal text: a password anyone can
  read in the manifest. The key is therefore `optional: false`, and the pod does not
  start without it.
- **An empty value.** `requirepass ""` turns the password off, and `optional: false`
  does not catch it, because the template ships `""` for every key. The
  `test -n "$REDIS_PASSWORD"` guard makes the Redis container exit instead of starting
  open.

## What this protects, and what it does not

It keeps other workloads in the cluster, and anything else that reaches port 6379,
out of Redis. It does not keep repository scripts out: until the sandbox transport moves
off Redis, every sandbox is handed `REDIS_URL`, password included, so a command running
inside a sandbox can read it.

The server never shows the URL: a startup finding or probe result about Redis names the
endpoints it parsed, or `(value hidden)`.
