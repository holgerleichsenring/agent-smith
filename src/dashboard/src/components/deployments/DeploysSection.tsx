"use client";

import Link from "next/link";
import type { DeployRecordView } from "@/lib/deploymentsApi";
import { DeployRecordCard } from "@/components/tickets/deploying/DeployRecordCard";
import { useT } from "@/hooks/useT";

// 2026-11-23-8df6a: the deploy runs agent smith made, each as the card its run's page shows, with a link to the run.

export function DeploysSection({ deploys }: { deploys: DeployRecordView[] }) {
  const t = useT();
  if (deploys.length === 0) return null;
  return (
    <section data-testid="deployments-deploys" className="scroll-mt-6">
      <div className="section-head">
        <h2>{t("deployments.deploys.title")}</h2>
        <span className="cnt" data-testid="deployments-deploys-count">{deploys.length}</span>
        <span className="sh-sub">{t("deployments.deploys.sub")}</span>
      </div>
      <div style={{ height: 6 }} />
      {deploys.map((d) => (
        <div key={d.runId} className="deployments-deploy" data-testid={`deployments-deploy-${d.runId}`}>
          <DeployRecordCard record={d} />
          <Link className="prog" href={`/jobs/${encodeURIComponent(d.runId)}`}>{t("deployments.run")}</Link>
        </div>
      ))}
    </section>
  );
}
