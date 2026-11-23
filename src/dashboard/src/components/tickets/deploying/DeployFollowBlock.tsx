"use client";

import { useEffect, useState } from "react";
import { getDeployRecord, type DeployRecordView } from "@/lib/deploymentsApi";
import { DeployRecordCard } from "./DeployRecordCard";

// 2026-11-18-3863f: where a deploy stands after its run ended — its pull request's merge and CI, or its pipeline run,
// as the server follows them; a red one with each failed check, its excerpt and link. Shown on a deploy run's page and
// under the deploy's line on its ticket. A run that delivered nothing shows nothing. 2026-11-23-8df6a: the card itself
// is DeployRecordCard, which the Deployments page shows too.

export function DeployFollowBlock({ runId }: { runId: string }) {
  const [record, setRecord] = useState<DeployRecordView | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    getDeployRecord(runId, abort.signal).then(setRecord).catch(() => setRecord(null));
    return () => abort.abort();
  }, [runId]);
  return record ? <DeployRecordCard record={record} /> : null;
}
