"use client";

import { use, useMemo } from "react";
import { useRouter } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunDetailSnapshot } from "@/hooks/useRunDetailSnapshot";
import { useRunSteps } from "@/hooks/useRunSteps";
import { RunLogView } from "@/components/jobs/runlog/RunLogView";
import { useRunJudgements } from "@/components/jobs/story/useRunJudgements";
import { deriveRunRepoNames } from "@/lib/runRepoNames";
import { RUNS_PATH } from "@/lib/RunBucketFilter";

// 2026-11-16-6877b: the run page as a sectioned text log.
// 2026-11-17-6d9ed: and only that — the one condensed log of the approved mock. The story view with its beats, the side
// rail, the dialogue drawer and the trace drawer with its rail and step panel are gone; what each showed lives in the
// log (see the inventory in .agentsmith/specs/done/2026-11-17-6d9ea-runlog-inventory-head-now.yaml). An old
// ?view=graph address simply shows the log.

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  return <RunDetail runId={decodeURIComponent(id)} />;
}

function RunDetail({ runId }: { runId: string }) {
  const router = useRouter();
  const { connectionState, overview } = useJobsHub();
  const events = useRunEvents(runId);

  const listSnapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);
  // p0344b: join the detail row — progressLedger/acceptance (and the other
  // detail-only fields) live on GET /api/runs/{id}, not the list payload.
  const snapshot = useRunDetailSnapshot(runId, listSnapshot);
  const repoNames = useMemo(() => deriveRunRepoNames(snapshot?.repos, events), [snapshot, events]);
  // p0388b: the steps come from the RunStep projection, complete on first paint and after a reload.
  const steps = useRunSteps(runId, snapshot);
  // 2026-08-25-e257: only fetched where there is a verdict to judge.
  const judged = useRunJudgements(runId, Boolean(snapshot?.acceptance));

  return (
    <div className="mock-shell mock-viewer">
      <RunLogView
        runId={runId}
        snapshot={snapshot}
        events={events}
        steps={steps}
        repoNames={repoNames}
        connectionState={connectionState}
        judged={judged}
        onDeleted={() => router.push(RUNS_PATH)}
      />
    </div>
  );
}
