"use client";

import { useMemo } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useCostRollup } from "@/hooks/useCostRollup";
import { useSpendBreakdown } from "@/hooks/useSpendBreakdown";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { deriveRunOutcomes } from "@/lib/runOutcomes";
import { PageHead } from "@/components/system/PageHead";
import { SpendSection } from "@/components/overview/SpendSection";
import { RunOutcomeSection } from "@/components/overview/RunOutcomeSection";
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
// different totals for one figure.
//
// The criteria section reads its own endpoint and fails independently: each
// section carries its own loading, empty and failed state, so a missing answer
// costs its box and not the page.

export function OverviewView() {
  const { overview } = useJobsHub();
  const cost = useCostRollup(overview);
  const slices = useSpendBreakdown(overview);
  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent) : []),
    [overview],
  );
  const outcomes = useMemo(() => deriveRunOutcomes(runs), [runs]);

  return (
    <div className="mock-shell mock-system" data-testid="overview-page">
      <main className="main">
        <PageHead
          title="Overview"
          sub="What the week cost, what came back, and how often a run met what was asked of it."
        />
        <SpendSection cost={cost} slices={slices} ready={overview !== null} />
        <RunOutcomeSection outcomes={outcomes} ready={overview !== null} />
        <ExpectationMetricsView />
      </main>
    </div>
  );
}
