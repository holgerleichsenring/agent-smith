"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import { pocRolloutPagePath } from "@/lib/pocRevisionPath";
import { fetchPocs, type PocRow } from "@/lib/pocsApi";

// 2026-11-23-c81ec: the run page of a poc-* run links its Rollout page. The PoC is the one whose last run this is, else
// the one whose project the run ran in; a caller who may not read the PoCs, or PoCs turned off, sees no link.

export const POC_PROJECT_PREFIX = "poc-";

/** The PoC a run of a poc-* project belongs to, or undefined. */
export function pocOfRun(pocs: PocRow[], runId: string, project: string): PocRow | undefined {
  return pocs.find((p) => p.lastRunId === runId) ?? pocs.find((p) => p.project === project);
}

export function PocRolloutLink({ runId, project }: { runId: string; project: string | null | undefined }) {
  const t = useT();
  const [pocId, setPocId] = useState<number | null>(null);
  const isPoc = !!project && project.startsWith(POC_PROJECT_PREFIX);

  useEffect(() => {
    setPocId(null);
    if (!isPoc || !project) return;
    const abort = new AbortController();
    fetchPocs(abort.signal)
      .then((read) => {
        if (abort.signal.aborted || read.status !== "on") return;
        setPocId(pocOfRun(read.list.pocs, runId, project)?.id ?? null);
      })
      .catch(() => { /* no link; the run page stands without it */ });
    return () => abort.abort();
  }, [isPoc, project, runId]);

  if (pocId === null) return null;
  return (
    <div className="rl-more">
      <Link className="rl-link" href={pocRolloutPagePath(pocId, runId)} data-testid="run-poc-rollout">{t("runs.pocRollout")} →</Link>
    </div>
  );
}
