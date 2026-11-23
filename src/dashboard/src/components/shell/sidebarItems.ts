import type { CallerIdentity } from "@/lib/identityApi";
import { holdsInAnyProject } from "@/lib/projectPermissions";
import { isPocOnly, RUNS_READ } from "@/lib/pocAccess";
import type { PocsStatus } from "@/hooks/usePocsFeature";

// 2026-11-23-933da: which sidebar items a caller sees, in rail order. An unknown caller (no identity read, as on an
// installation without sign-in) sees everything as before — the server decides. A known caller loses what they
// cannot read: the work items and Insight without runs.read anywhere, Work it out without dialog.write anywhere. A
// PoC-only caller sees PoCs alone. PoCs shows only when GET /api/pocs answered 200.
//
// The order after Pull requests is Pull requests, Deployments (2026-11-23-8df6a), PoCs. Deployments is gated like
// Pull requests (runs.read anywhere).

export type SidebarItemId =
  | "new-work" | "work-it-out" | "series" | "your-move" | "tickets" | "runs" | "pull-requests" | "deployments" | "pocs"
  | "insight" | "overview" | "delivery" | "recent";

const DIALOG_WRITE = "dialog.write";
const WORK: SidebarItemId[] = ["series", "your-move", "tickets", "runs", "pull-requests", "deployments"];
const INSIGHT: SidebarItemId[] = ["insight", "overview", "delivery"];

export function sidebarItems(identity: CallerIdentity | null, pocs: PocsStatus): SidebarItemId[] {
  const pocsItem: SidebarItemId[] = pocs === "on" ? ["pocs"] : [];
  if (isPocOnly(identity)) return pocsItem;
  const reads = identity === null || holdsInAnyProject(identity, RUNS_READ);
  const designs = identity === null || holdsInAnyProject(identity, DIALOG_WRITE);
  return [
    "new-work",
    ...(designs ? (["work-it-out"] as SidebarItemId[]) : []),
    ...(reads ? WORK : []),
    ...pocsItem,
    ...(reads ? INSIGHT : []),
    "recent",
  ];
}
