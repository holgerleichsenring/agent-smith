# Phase 19a-1: IJobSpawner Interface + KubernetesJobSpawner

## Goal

Extract a clean `IJobSpawner` interface from the existing `JobSpawner` class.
Rename `JobSpawner` to `KubernetesJobSpawner`. This enables `DockerJobSpawner`
to be added without touching any existing logic.

---

## What & Why

**Problem:** `JobSpawner` was tightly coupled to Kubernetes. No abstraction existed
to swap in a different spawner for local development.

**Solution:** Standard interface extraction. `IJobSpawner` has a single method:
`SpawnAsync(FixTicketIntent, CancellationToken) → Task<string>` returning the jobId.

**Side effect removed:** The `K8S_REDIS_URL` hack is eliminated. `KubernetesJobSpawner`
now uses `REDIS_URL` directly — in K8s mode, Redis runs in the same cluster.

---

## Files

| File | Change |
|------|--------|
| `Services/IJobSpawner.cs` | NEW — single-method interface |
| `Services/JobSpawner.cs` | RENAMED → `KubernetesJobSpawner.cs` |
| `Services/KubernetesJobSpawner.cs` | Class + logger renamed, implements `IJobSpawner`, `K8S_REDIS_URL` removed |
| `Services/KubernetesJobSpawner.cs` | `JobSpawnerOptions` extended with `DockerNetwork` property (used only by DockerJobSpawner) |

---

## Success Criteria

- [ ] `IJobSpawner` defined with single `SpawnAsync` method
- [ ] `KubernetesJobSpawner` implements `IJobSpawner`
- [ ] No references to `JobSpawner` remain in the codebase
- [ ] `K8S_REDIS_URL` does not appear anywhere in the codebase
- [ ] `dotnet build` succeeds