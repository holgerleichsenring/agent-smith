"use client";

import { useEffect, useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { listDeployments, type DeploymentBindingView } from "@/lib/deploymentsApi";
import { RUNS_CONTROL } from "../detail/RetryUnchanged";
import { DeployForm } from "./DeployForm";

// 2026-11-18-3863d: Deploy — a context of the ticket's project to one of its bound environments. Shown only to a caller
// with runs.control in the project, and only when the project binds a deployment template; the run it starts is
// listed on this ticket's page.

export function DeployAct({ project, ticketId, onStarted }: { project: string; ticketId: string; onStarted: () => void }) {
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [bindings, setBindings] = useState<DeploymentBindingView[]>([]);
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (lacks) return;
    const abort = new AbortController();
    listDeployments(project, abort.signal).then(setBindings).catch(() => setBindings([]));
    return () => abort.abort();
  }, [project, lacks]);
  if (lacks || bindings.length === 0) return null;
  return (
    <span className="deploy-act" data-testid="deploy-act">
      {!open && (
        <button type="button" className="btn" onClick={() => setOpen(true)} data-testid="deploy-open">Deploy…</button>
      )}
      {open && <DeployForm project={project} ticketId={ticketId} bindings={bindings} onStarted={onStarted} />}
    </span>
  );
}
