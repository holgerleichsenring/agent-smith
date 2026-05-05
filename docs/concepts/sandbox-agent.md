# Sandbox Agent

The **Sandbox Agent** is a small, standalone .NET executable that runs inside
each per-pipeline sandbox pod. It pulls Steps from Redis, executes them as
shell commands, streams stdout/stderr back, and exits when told to.

It is deliberately **boring**: no provider knowledge, no pipeline knowledge,
no opinion on what languages or toolchains exist. The sandbox agent ships as
**one carrier image** — `agent-smith-sandbox-agent` — that injects its own
binary into any official toolchain image via the **init-container pattern**.

## What it is

- A worker process inside a sandbox pod
- Driven by a Redis wire format (Step / StepEvent / StepResult)
- Self-contained .NET 8 single-file binary, no runtime dependencies beyond glibc
- One pipeline = one pod = one agent process (run-once, exit on Shutdown)

## What it isn't

- Not a daemon — no multi-job pooling, no queue server
- Not toolchain-aware — it runs whatever shell command Step#1 says
- Not source-aware — `git clone` (or any other source acquisition) is just
  a Step the Server pod composes
- Not a pre-baked SDK image — see *Why init-container, not pre-baked* below

## The init-container injection pattern

Each sandbox pod has **two containers** sharing an `emptyDir` volume:

```
┌──────────────────── Pod ─────────────────────┐
│                                              │
│  initContainer: agent-smith-sandbox-agent    │
│  ┌────────────────────────────────────────┐  │
│  │  ENTRYPOINT: /agent --inject /shared   │  │
│  │  → cp /agent /shared/agent (exit 0)    │  │
│  └────────────────────────────────────────┘  │
│             │                                │
│             ▼ writes /shared/agent           │
│  ┌────────────── emptyDir /shared ─────────┐ │
│  │  agent (executable, ~80 MB)             │ │
│  └─────────────────────────────────────────┘ │
│             ▲ reads /shared/agent            │
│  ┌────────────────────────────────────────┐  │
│  │  main: mcr.microsoft.com/dotnet/sdk:8  │  │
│  │  command: ['/shared/agent']            │  │
│  │  args: ['--redis-url', '...',          │  │
│  │         '--job-id', 'pipe-42']         │  │
│  │  → JobLoop pulls Steps from Redis      │  │
│  └────────────────────────────────────────┘  │
│                                              │
└──────────────────────────────────────────────┘
```

Sample Pod spec the Server pod will produce (in p0116):

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: sandbox-pipe-42
spec:
  restartPolicy: Never
  volumes:
    - name: shared
      emptyDir: {}
    - name: work
      emptyDir: {}
  initContainers:
    - name: inject-agent
      image: holgerleichsenring/agent-smith-sandbox-agent:1.0.0
      volumeMounts:
        - name: shared
          mountPath: /shared
      # default CMD already runs `--inject /shared/agent`
  containers:
    - name: toolchain
      image: mcr.microsoft.com/dotnet/sdk:8.0   # <-- unmodified upstream
      command: ['/shared/agent']
      args:
        - --redis-url
        - redis://redis.agentsmith.svc.cluster.local:6379
        - --job-id
        - pipe-42
      env:
        - name: GIT_TOKEN
          valueFrom:
            secretKeyRef:
              name: agentsmith-secrets
              key: github-token
      volumeMounts:
        - name: shared
          mountPath: /shared
        - name: work
          mountPath: /work
      workingDir: /work
```

## Why init-container, not pre-baked images

A pre-baked-image strategy would mean we publish `sandbox-dotnet`,
`sandbox-node`, `sandbox-java`, `sandbox-salesforce-cli` … one image per
language we want to support. We **rejected** that approach because:

| Concern                     | Pre-baked images        | Init-container injection |
| --------------------------- | ----------------------- | ------------------------ |
| Images we maintain          | One per toolchain       | Exactly one (the agent)  |
| Toolchain version updates   | We rebuild on every release | Operator pulls upstream |
| Operator-custom toolchains  | Operator forks our image    | Operator references their own image directly |
| Adding a new language       | Publish a new image     | Zero changes from us     |
| Image size per pod          | ~900 MB (SDK + agent)   | ~80 MB carrier + upstream image (cached on the node) |

The cost is one extra container per pod (the initContainer), which K8s
handles in milliseconds.

## Redis wire format

Three keys per job, all under the `sandbox:{jobId}:` namespace:

| Key            | Type   | Direction         | Purpose            |
| -------------- | ------ | ----------------- | ------------------ |
| `…:in`         | LIST   | Server → Agent    | Steps to execute   |
| `…:events`     | STREAM | Agent → consumers | Soft-batched stdout/stderr lines |
| `…:results`    | LIST   | Agent → Server    | One StepResult per Step |

### Step (input)

```json
{
  "schemaVersion": 1,
  "stepId": "11111111-1111-1111-1111-111111111111",
  "kind": "run",
  "command": "git",
  "args": ["clone", "https://github.com/foo/bar.git", "."],
  "workingDirectory": "/work",
  "env": null,
  "timeoutSeconds": 600
}
```

`kind` is `run` (default) or `shutdown`. Run steps require `command`. The
agent inherits its pod's environment, so secrets like `GIT_TOKEN` are
available without putting them in `step.env` (which would land in Redis
and be readable via `redis-cli`).

### StepEvent (output)

```json
{
  "schemaVersion": 1,
  "stepId": "11111111-1111-1111-1111-111111111111",
  "kind": "stdout",
  "line": "Cloning into '.'...",
  "timestamp": "2026-05-05T10:00:00.123+00:00"
}
```

`kind` is one of `started`, `stdout`, `stderr`, `completed`. Events are
soft-batched (50 lines OR 100 ms, whichever fires first) for efficiency
without sacrificing live-progress feel.

### StepResult (output)

```json
{
  "schemaVersion": 1,
  "stepId": "11111111-1111-1111-1111-111111111111",
  "exitCode": 0,
  "timedOut": false,
  "durationSeconds": 1.23,
  "errorMessage": null
}
```

## Lifecycle

```
boot
 → connect to Redis (5 reconnect attempts, AbortOnConnectFail=false)
 → loop:
     LPOP sandbox:{jobId}:in  (60 s deadline)
       null?  → idle cycle (max 5 then exit 2)
       Shutdown? → exit 0
       Run?   → execute, stream events, push StepResult
 → on SIGINT/SIGTERM: cancel loop, dispose bus, exit
 → on unhandled exception: log to stderr, exit 3
```

## Local debugging

### Smoke 1 — agent against a local Redis

```bash
docker run --rm -d -p 6379:6379 redis:7-alpine

dotnet run --project src/AgentSmith.Sandbox.Agent -- \
  --redis-url redis://localhost:6379 \
  --job-id smoke-test \
  --verbose

# in another terminal:
redis-cli LPUSH sandbox:smoke-test:in '{
  "schemaVersion":1,
  "stepId":"00000000-0000-0000-0000-000000000001",
  "kind":"run",
  "command":"echo",
  "args":["hello"],
  "timeoutSeconds":10
}'

redis-cli XRANGE sandbox:smoke-test:events - +
redis-cli LRANGE sandbox:smoke-test:results 0 -1

redis-cli LPUSH sandbox:smoke-test:in '{
  "schemaVersion":1,
  "stepId":"00000000-0000-0000-0000-000000000002",
  "kind":"shutdown"
}'
# agent exits 0
```

### Smoke 2 — init-container injection

Mimics the K8s two-container pattern using docker volumes:

```bash
docker build -t agent-smith-sandbox-agent:smoke \
  -f src/AgentSmith.Sandbox.Agent/Dockerfile .

docker volume create as-shared

# init-container behavior: copy binary into the shared volume
docker run --rm -v as-shared:/shared agent-smith-sandbox-agent:smoke

# main container behavior: run injected binary inside an UNMODIFIED upstream image
docker run --rm --network=host -v as-shared:/shared \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  /shared/agent --redis-url redis://localhost:6379 \
                --job-id inject-smoke --verbose
```

If Smoke 2 succeeds against an unmodified upstream image, the
init-container pattern works end-to-end without ever touching K8s.
