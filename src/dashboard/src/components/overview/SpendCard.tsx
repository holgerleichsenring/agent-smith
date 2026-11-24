import type { CostRollup } from "@/hooks/useCostRollup";
import { OverviewCard } from "@/components/overview/OverviewCard";
import { useDateLocale, useT } from "@/hooks/useT";

// 2026-08-27-559e: the spend card — the trailing week is the headline, and the
// two figures that stood beside it in the strip name themselves beneath it.
// Every one is the figure useCostRollup already read; folding them into one
// card changes where they sit, not what they say.

export function SpendCard({ cost, ready }: { cost: CostRollup; ready: boolean }) {
  const t = useT();
  return (
    <OverviewCard
      label={t("overview.spend.label")}
      value={ready ? <span data-testid="kcard-cost-week">${cost.week.toFixed(2)}</span> : "—"}
      detail={ready ? <SpendDetail cost={cost} /> : t("overview.spend.reading")}
      testId="overview-spend-card"
    />
  );
}

function SpendDetail({ cost }: { cost: CostRollup }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <>
      <span data-testid="kcard-cost-today">${cost.today.toFixed(2)}</span> {t("overview.spend.today")} ·{" "}
      <span data-testid="kcard-cost-calls-7d">{cost.llmCalls.toLocaleString(locale)}</span> {t("overview.spend.calls")}
    </>
  );
}
