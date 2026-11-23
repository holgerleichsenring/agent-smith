import type { RunOutcomes } from "@/lib/runOutcomes";
import { OverviewCard } from "@/components/overview/OverviewCard";
import { useT } from "@/hooks/useT";

// 2026-08-27-559e: what came back — the total as the headline, with the
// finished runs split by how they ended beneath it. The in-flight buckets the
// seven-cell strip also carried (needs you, running, queued) are what the rail
// counts on every page; repeating them here was the reading the operator
// rejected as a rail count restated.

export function RunOutcomeCard({ outcomes, ready }: { outcomes: RunOutcomes; ready: boolean }) {
  const t = useT();
  return (
    <OverviewCard
      label={t("shell.nav.runs")}
      value={ready ? <span data-testid="kcard-runs-total">{outcomes.total}</span> : "—"}
      detail={ready ? <OutcomeDetail outcomes={outcomes} /> : <LoadingDetail />}
      testId="overview-runs-card"
    />
  );
}

function OutcomeDetail({ outcomes }: { outcomes: RunOutcomes }) {
  const t = useT();
  return (
    <>
      <span data-testid="kcard-runs-succeeded">{outcomes.succeeded}</span> {t("overview.runs.succeeded")} ·{" "}
      <span data-testid="kcard-runs-shortfall">{outcomes.shortfall}</span> {t("overview.runs.shortfall")} ·{" "}
      <span data-testid="kcard-runs-failed">{outcomes.failed}</span> {t("overview.runs.failed")} ·{" "}
      <span data-testid="kcard-runs-cancelled">{outcomes.cancelled}</span> {t("overview.runs.cancelled")}
    </>
  );
}

function LoadingDetail() {
  const t = useT();
  return <span data-testid="overview-runs-loading">{t("overview.runs.reading")}</span>;
}
