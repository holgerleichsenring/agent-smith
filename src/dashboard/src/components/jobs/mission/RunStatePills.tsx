"use client";

import { StatePills, type StatePill } from "@/components/ui/StatePills";
import { bucketHref, useRunBucketFilter, type RunBucketFilter } from "@/lib/RunBucketFilter";
import type { MissionBuckets } from "./missionBuckets";

// 2026-11-16-0440b: the runs list's buckets as pills, counted from the same bucketing the sections
// below render, so a pill and its section cannot disagree. They replace the rail's monitor items.

export function runPills(buckets: MissionBuckets): StatePill<RunBucketFilter>[] {
  const total = buckets.needsYou.length + buckets.running.length + buckets.queued.length + buckets.finished.length;
  return [
    { key: "all", label: "All", href: bucketHref("all"), count: total },
    { key: "needs-you", label: "Waiting for input", href: bucketHref("needs-you"), count: buckets.needsYou.length, tone: "needs" },
    { key: "running", label: "Running", href: bucketHref("running"), count: buckets.running.length, tone: "proven" },
    { key: "queued", label: "Queued", href: bucketHref("queued"), count: buckets.queued.length },
    { key: "finished", label: "Finished", href: bucketHref("finished"), count: buckets.finished.length },
  ];
}

export function RunStatePills({ buckets }: { buckets: MissionBuckets }) {
  const { filter, select } = useRunBucketFilter();
  return <StatePills label="Run state" pills={runPills(buckets)} selected={filter} onSelect={select} testId="run-pills" />;
}
