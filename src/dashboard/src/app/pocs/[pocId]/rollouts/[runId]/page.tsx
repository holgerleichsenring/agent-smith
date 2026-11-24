"use client";

import { use } from "react";
import { PocRolloutPage } from "@/components/pocs/rollout/PocRolloutPage";

// 2026-11-23-c81ec: the rollout of one PoC run — its steps and live lines — in the PoCs list's frame.

interface PageProps {
  params: Promise<{ pocId: string; runId: string }>;
}

export default function PocRolloutRoute({ params }: PageProps) {
  const { pocId, runId } = use(params);
  const id = Number(decodeURIComponent(pocId));
  const run = decodeURIComponent(runId);
  return (
    <div className="mock-shell mock-tickets mock-pocs" data-testid="poc-rollout-page">
      <main className="main">
        <PocRolloutPage key={`${id}/${run}`} pocId={id} runId={run} />
      </main>
    </div>
  );
}
