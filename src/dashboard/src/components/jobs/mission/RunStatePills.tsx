"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { StatePills, type StatePill } from "@/components/ui/StatePills";
import { bucketHref, useRunBucketFilter, type RunBucketFilter } from "@/lib/RunBucketFilter";
import type { MissionBuckets } from "./missionBuckets";

// 2026-11-16-0440b: the runs list's buckets as pills, counted from the same bucketing the sections
// below render, so a pill and its section cannot disagree. They replace the rail's monitor items.

export function runPills(t: T, buckets: MissionBuckets): StatePill<RunBucketFilter>[] {
  const total = buckets.needsYou.length + buckets.running.length + buckets.queued.length + buckets.finished.length;
  return [
    { key: "all", label: t("runs.mission.all"), href: bucketHref("all"), count: total },
    { key: "needs-you", label: t("runs.mission.needsYou"), href: bucketHref("needs-you"), count: buckets.needsYou.length, tone: "needs" },
    { key: "running", label: t("runs.mission.running"), href: bucketHref("running"), count: buckets.running.length, tone: "proven" },
    { key: "queued", label: t("runs.mission.queued"), href: bucketHref("queued"), count: buckets.queued.length },
    { key: "finished", label: t("runs.mission.finished"), href: bucketHref("finished"), count: buckets.finished.length },
  ];
}

export function RunStatePills({ buckets }: { buckets: MissionBuckets }) {
  const t = useT();
  const { filter, select } = useRunBucketFilter();
  return <StatePills label={t("runs.mission.pillsLabel")} pills={runPills(t, buckets)} selected={filter} onSelect={select} testId="run-pills" />;
}
