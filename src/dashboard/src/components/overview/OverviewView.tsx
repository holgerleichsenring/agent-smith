"use client";

import { useMemo } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useCostRollup } from "@/hooks/useCostRollup";
import { useSpendBreakdown } from "@/hooks/useSpendBreakdown";
import { useExpectationMetrics } from "@/hooks/useExpectationMetrics";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { deriveRunOutcomes } from "@/lib/runOutcomes";
import { PageHead } from "@/components/system/PageHead";
import { SpendCard } from "@/components/overview/SpendCard";
import { RunOutcomeCard } from "@/components/overview/RunOutcomeCard";
import { CriteriaMetCard } from "@/components/overview/CriteriaMetCard";
import { SpendBreakdownPanel } from "@/components/overview/SpendBreakdownPanel";
import { ExpectationMetricsView } from "@/components/system/ExpectationMetricsView";

// 2026-08-27-7463: the Overview — spend, what came back, and how often runs met
// what was asked of them, on one page. It replaces three rollup pages of three
// numbers each: apart, none was worth opening; together the comparison an
// operator actually makes (what was spent against what came back) is on one
// screen.
//
// The run overview is read ONCE, here, and passed down. useJobsHub is per mount
// — its own fetch and its own SubscribeOverview each time — so two sections
// each calling it would cost two more of both per page load and could show two
// different totals for one figure. The criteria read is lifted here for the
// same reason, because the card and the panel show the same outcomes.
//
// 2026-08-27-559e: the page is a row of three CARDS over a row of two PANELS,
// inside a bounded column. Stacked full-width strips were the three thin pages
// re-stacked, which is the complaint the page existed to answer. Every figure
// is the one the sections already read — this phase moved them, it did not
// recompute them.

export function OverviewView() {
  const { overview } = useJobsHub();
  const cost = useCostRollup(overview);
  const slices = useSpendBreakdown(overview);
  const criteria = useExpectationMetrics();
  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent) : []),
    [overview],
  );
  const outcomes = useMemo(() => deriveRunOutcomes(runs), [runs]);
  const ready = overview !== null;

  return (
    <div className="mock-shell mock-system mock-overview" data-testid="overview-page">
      <main className="main">
        <PageHead
          title="Overview"
          sub="What the week cost, what came back, and how often a run met what was asked of it."
        />
        <div className="ov-cards" data-testid="overview-cards">
          <SpendCard cost={cost} ready={ready} />
          <RunOutcomeCard outcomes={outcomes} ready={ready} />
          <CriteriaMetCard read={criteria} />
        </div>
        <div className="ov-panels" data-testid="overview-panels">
          <SpendBreakdownPanel slices={slices} ready={ready} />
          <ExpectationMetricsView {...criteria} />
        </div>
      </main>
    </div>
  );
}
