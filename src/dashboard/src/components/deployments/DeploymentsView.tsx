"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { fetchDeploymentsPage, type DeploymentsPageView } from "@/lib/deploymentsApi";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { NoProjectReach } from "@/components/shell/NoProjectReach";
import { useT } from "@/hooks/useT";
import "@/styles/deployments.css";
import { DAYS_PARAM, DEPLOYMENT_WINDOWS, windowHref, windowOf, type DeploymentWindow } from "./deploymentsWindow";
import { MergedSection } from "./MergedSection";
import { DeploysSection } from "./DeploysSection";

// 2026-11-23-8df6a: after the merge — the merged pull requests of the caller's projects with the commit each merge
// became, and the deploys agent smith ran, over 7, 30 or 90 days. Read when the page opens and when the span changes.

export const DEPLOYMENTS_PATH = "/deployments";

export function DeploymentsView() {
  const t = useT();
  const router = useRouter();
  const days = windowOf(useSearchParams()?.get(DAYS_PARAM) ?? null);
  const [view, setView] = useState<DeploymentsPageView | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const abort = new AbortController();
    setView(null);
    setError(null);
    fetchDeploymentsPage(days, abort.signal).then(setView).catch((e: unknown) => {
      if (!abort.signal.aborted) setError(e instanceof Error ? e : new Error(String(e)));
    });
    return () => abort.abort();
  }, [days]);

  const choose = (next: DeploymentWindow) => router.replace(windowHref(DEPLOYMENTS_PATH, next), { scroll: false });
  return (
    <div className="mock-shell mock-runs mock-deployments" data-testid="deployments-page">
      <main className="main">
        <div className="m-head">
          <div>
            <h1>{t("deployments.title")}</h1>
            <div className="msub">{t("deployments.sub")}</div>
          </div>
          <div className="seg" role="group" aria-label={t("deployments.windowLabel")}>
            {DEPLOYMENT_WINDOWS.map((w) => (
              <button key={w} type="button" aria-pressed={w === days} data-testid={`deployments-days-${w}`} onClick={() => choose(w)}>
                {t("deployments.window", { count: w })}
              </button>
            ))}
          </div>
        </div>
        <DeploymentsBody view={view} error={error} />
      </main>
    </div>
  );
}

function DeploymentsBody({ view, error }: { view: DeploymentsPageView | null; error: Error | null }) {
  const t = useT();
  const refusal = refusalIn(error);
  if (refusal) return <RefusalSurface refusal={refusal} surface={t("deployments.surface")} />;
  if (error) return <p className="msub" data-testid="deployments-error">{t("deployments.failed", { message: error.message })}</p>;
  if (!view) return <p className="msub" data-testid="deployments-loading">{t("deployments.loading")}</p>;
  if (view.merged.length === 0 && view.deploys.length === 0)
    return (
      <div className="rows" data-testid="deployments-empty">
        <div className="rrow" style={{ cursor: "default", justifyContent: "center", display: "flex" }}>
          <NoProjectReach>{t("deployments.empty")}</NoProjectReach>
        </div>
      </div>
    );
  return (
    <>
      <MergedSection merged={view.merged} />
      <DeploysSection deploys={view.deploys} />
    </>
  );
}
