# Docker + Kubernetes Troubleshooting Guide

The failures you are most likely to meet when you host the server yourself, with the
message you'll see and what to do about it. Most of them are also caught earlier by
`agent-smith doctor` or the startup preflight on `/health`; when something looks off,
look there first.

## The server can't reach Redis

```
StackExchange.Redis.RedisConnectionException:
It was not possible to connect to the redis server(s).
```

`REDIS_URL` is `host:port`, not a URI. `redis:6379` works, `redis://redis:6379` does not.

## Runs never start on Docker: no socket

```
Docker socket not available: … Mount /var/run/docker.sock into the dispatcher container.
```

With `SPAWNER_TYPE=docker` the server creates containers through the host's Docker
daemon, so the compose service needs `/var/run/docker.sock` mounted (the example
compose file does this, and runs the server as root for socket access).

## The sandbox agent image is missing

```
Sandbox agent image 'agent-smith-sandbox-agent:<tag>' not found locally and not pullable from a registry.
```

The carrier image isn't on the host and couldn't be pulled. Pull
`holgerleichsenring/agent-smith-sandbox-agent:<tag>`, or build it once with
`docker compose -f deploy/docker-compose.yml --profile build-only build sandbox-agent`.
Check that the tag in the message is the one you expect: it is derived from the
server's release unless `sandbox.agent_version` or `deployment.version` pins it.

## "The sandbox agent version is normally derived from the release this server is"

The server was built without `AGENTSMITH_RELEASE_VERSION`, so it can't name its own
release and has nothing to derive the agent tag from. That happens with images you
build yourself. Either build with the identity passed in (see
[docker-compose](../../host-it/docker-compose.md#building-the-images-yourself)) or set
`deployment.version` to a published tag.

## A toolchain image can't be pulled on Docker

```
Docker could not pull image '…'. The Docker sandbox backend carries no registry pull credential
```

`sandbox.image_pull_secrets` is Kubernetes-only. On Docker, pull the image onto the
host yourself (`docker login` + `docker pull`), or run sandboxes on Kubernetes.

## The checkout fails: the image has no git

A repository is cloned inside its own sandbox, so the toolchain image must carry git.
The checkout step says so by name when it doesn't. Name an image variant that ships git
in the context's `stack.image`, or in `projects.<name>.sandbox.toolchain_image` /
`sandbox.images`.

## The context's image is ignored

```
context.yaml stack.image '…' is outside the trusted registries [...]. Falling back for lang=….
```

The image isn't under `sandbox.allowed_registries` (default `mcr.microsoft.com/` and
`ghcr.io/`), or it is a Docker Hub library image while a registry list is set and
`allow_docker_hub_library` isn't. Widen the list, or name the image yourself in
`toolchain_image` / `images`, which are not checked. See
[Kubernetes](../../host-it/kubernetes.md#which-registries-a-toolchain-image-may-come-from).

## A declared credential never arrives

The run preflight's `declared-secrets` check names a `sandbox.secrets` entry that is
malformed or clashes; `injected-secrets` names a variable or file that didn't arrive in
the pod, never its value. On Docker or in-process it warns "NOT INJECTED" instead:
those backends inject no secrets at all.

## A command dies at a round number of seconds

A command killed at the step cap is reported as timed out. The default for
`run_command` is `run_command_timeout_seconds` (300); a command may ask for more, but
never past `step_timeout_seconds` (900). Raise the cap to let long builds finish.

## Leftover sandbox containers pile up

On startup a server that won't reap says so:

```
SandboxOrphanReaper is NOT running: … set SANDBOX_ORPHAN_REAPER=true to run it anyway.
```

The reaper only runs where a database-backed lease can tell live runs from dead ones.
Set `SANDBOX_ORPHAN_REAPER=true` to force it. With two deployments on one host, each
reaps only its own containers, keyed by the Redis endpoint; if two deployments share a
Redis on purpose but should not share cleanup, give each a `SANDBOX_OWNER_ID`.

## Restores are slow every time

Check that `SANDBOX_PACKAGE_CACHE` isn't `false` on the server, and that the
`agentsmith-pkgcache-*` volumes exist (`docker volume ls`). A corrupted cache for one
ecosystem is cleared with `docker volume rm agentsmith-pkgcache-<ecosystem>`.

## The server keeps restarting on Kubernetes

Usually an OOMKill. Give the server pod a request of at least 512Mi and a limit of
1–1.5Gi (see [Kubernetes → Resources](../../host-it/kubernetes.md#resources)). Every
restart reaps the in-flight run.

## The CLI init-container fails under a hardened `securityContext`

The CLI image's entrypoint drops privileges with gosu, which a read-only, non-root
`securityContext` refuses. Call `dotnet AgentSmith.Cli.dll …` directly as the command.

## The server can't reach the Kubernetes API from Docker Desktop

Outside a cluster the server reads the kubeconfig and rewrites Docker Desktop's
`https://127.0.0.1:` to `https://host.docker.internal:` itself, because inside a
container `127.0.0.1` is the container. On Linux that name only resolves when it is
mapped: `extra_hosts: ["host.docker.internal:host-gateway"]`, as the example compose
file does.

## Quick diagnostics

```bash
curl http://localhost:8081/health                          # server + preflight report
docker compose -f deploy/docker-compose.yml logs -f server  # watch a run start
docker compose -f deploy/docker-compose.yml exec redis redis-cli ping   # PONG
docker ps --filter label=agent-smith.job-id                # sandbox containers on this host
kubectl -n agentsmith get pods -l app=agentsmith-sandbox    # sandbox pods
```
