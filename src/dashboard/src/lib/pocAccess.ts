import type { CallerIdentity } from "@/lib/identityApi";

// 2026-11-23-933da: a PoC-only caller — one whose only work here is their PoCs. They hold pocs.read globally, no
// global runs.read, and every project that grants them runs.read is a poc- project (the per-person project the
// server binds them to). The sidebar shows such a caller only PoCs, and / sends them to /pocs.

export const POCS_READ = "pocs.read";
export const RUNS_READ = "runs.read";
export const POC_PROJECT_PREFIX = "poc-";

export function isPocOnly(identity: CallerIdentity | null): boolean {
  if (!identity) return false;
  if (!identity.permissions.includes(POCS_READ) || identity.permissions.includes(RUNS_READ)) return false;
  return Object.entries(identity.projectPermissions ?? {})
    .filter(([, granted]) => granted.includes(RUNS_READ))
    .every(([project]) => project.toLowerCase().startsWith(POC_PROJECT_PREFIX));
}
