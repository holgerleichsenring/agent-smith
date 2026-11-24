"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import type { RunSnapshot } from "@/types/hub-events";
import { fetchRunsBefore } from "@/lib/runsApi";
import { ConnectionState } from "./ConnectionState";
import { RunRow } from "./RunRow";
import { mergeNewestFirst } from "@/lib/runs/mergeNewestFirst";
import { ClearTerminalRunsButton } from "./ClearTerminalRunsButton";
import { SystemRunsToggle } from "./mission/SystemRunsToggle";
import { loadMoreCursor, oldestStart, splitSystemRuns } from "@/lib/runs/systemRuns";
import { useShowSystemRuns } from "@/hooks/useShowSystemRuns";
import { bucketRuns, deriveMetrics } from "./mission/missionBuckets";
import { MetricStrip } from "./mission/MetricStrip";
import { RunStatePills } from "./mission/RunStatePills";
import { Section } from "./mission/Section";
import { useRunBucketFilter, type RunBucket } from "@/lib/RunBucketFilter";
import { RenderBoundary } from "@/components/shell/RenderBoundary";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { NoProjectReach } from "@/components/shell/NoProjectReach";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

// p0343: mission control — the home screen ranks runs by what needs the
// operator: Waiting for input → Running → Queued → Finished.
// 2026-10-13-9a59p: a parked run is a plain row here. A ticket's question is
// answered on the ticket list, a ticketless run's in the run viewer. Empty sections are omitted so the screen shows only live
// buckets — except a reassuring "nothing waiting on you" when the top priority
// is clear.
// p0343c (pixel identity): emits the runs-list.html DOM verbatim — .health
// strip, .section-head slim rules with .cnt pills and .sh-sub hints, .need
// cards, .rows of .rrow rows.
// p0458: the state pills (2026-11-16-0440b) narrow this to ONE bucket. A chosen bucket always renders,
// empty or not — omitting it would answer the operator's click with a blank page.

// What each bucket says when the operator asked for it and it holds nothing.
const EMPTY_LINE: Record<RunBucket, TextKey> = {
  "needs-you": "runs.mission.empty.needsYou",
  running: "runs.mission.empty.running",
  queued: "runs.mission.empty.queued",
  finished: "runs.mission.empty.finished",
};

export function MissionControl() {
  const t = useT();
  const { filter } = useRunBucketFilter();
  const { connectionState, overview, refusal } = useJobsHub();
  // p0355: the runs list is no longer hard-capped. `older` holds runs paged in
  // beyond the live window via "Load more"; they merge with the live overview
  // (live wins on id) so the Finished section can reach every run, newest-first.
  const [older, setOlder] = useState<RunSnapshot[]>([]);
  const [loadingMore, setLoadingMore] = useState(false);
  const [exhausted, setExhausted] = useState(false);

  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, [...overview.recent, ...older]) : []),
    [overview, older],
  );
  // 2026-11-24-35ff: the runs the server starts itself are hidden unless the viewer asks; sections, pills and metrics
  // count what is shown. Load more still pages from the oldest run loaded, shown or not.
  const [showSystem, setShowSystem] = useShowSystemRuns();
  const split = useMemo(() => splitSystemRuns(runs, showSystem), [runs, showSystem]);
  const buckets = useMemo(() => bucketRuns(split.shown), [split]);
  const metrics = useMemo(() => deriveMetrics(split.shown), [split]);
  // Load more pages through every finished run, so it stays offered when the loaded ones are all hidden system runs.
  const finishedLoaded = useMemo(() => bucketRuns(runs).finished.length, [runs]);

  // 2026-11-24-6357: the cursor Load more pages from. It starts at the end of the loaded windows and then follows each page's
  // oldest run, so a page of runs already loaded moves it on instead of ending the list; only an empty page does. Turning
  // the toggle changes what it pages through, so it starts over.
  const [pageCursor, setPageCursor] = useState<string | null>(null);
  useEffect(() => {
    setExhausted(false);
    setPageCursor(null);
  }, [showSystem]);

  const loadMore = useCallback(async () => {
    const cursor = pageCursor ?? loadMoreCursor(runs, showSystem);
    if (loadingMore || cursor === null) return;
    setLoadingMore(true);
    try {
      const page = await fetchRunsBefore(cursor, 20, undefined, showSystem);
      const known = new Set(runs.map((r) => r.runId));
      const fresh = page.filter((r) => !known.has(r.runId));
      const next = oldestStart(page);
      if (page.length === 0 || next === null) setExhausted(true);
      else setPageCursor(next);
      if (fresh.length > 0) setOlder((prev) => [...prev, ...fresh]);
    } catch {
      // Endpoint unavailable / transient — stop offering to page further.
      setExhausted(true);
    } finally {
      setLoadingMore(false);
    }
  }, [loadingMore, runs, showSystem, pageCursor]);

  // 2026-09-21-291b: a skeleton that cannot resolve. With nothing read and the
  // server refusing this caller, the pulse below was the whole screen — for as
  // long as the tab stayed open — and the one action that resolves it was never
  // offered. RefusalSurface is the answer the REST path already had.
  if (overview === null && refusal) {
    return (
      <div className="space-y-4" data-testid="mission-refused">
        <ConnectionState state={connectionState} refusal={refusal} />
        <RefusalSurface refusal={refusal} surface={t("runs.mission.yourRuns")} />
      </div>
    );
  }

  if (overview === null && connectionState !== HubConnectionState.Connected) {
    return (
      <div className="space-y-4" data-testid="mission-skeleton">
        <ConnectionState state={connectionState} refusal={refusal} />
        <div className="health">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="metric h-16 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (runs.length === 0) {
    return (
      <div className="space-y-4" data-testid="mission-empty">
        <ConnectionState state={connectionState} refusal={refusal} />
        <div className="rows">
          <div className="rrow" style={{ cursor: "default", justifyContent: "center", display: "flex" }}>
            <NoProjectReach>{t("runs.mission.noRuns")}</NoProjectReach>
          </div>
        </div>
      </div>
    );
  }

  const shows = (bucket: RunBucket) => filter === "all" || filter === bucket;

  return (
    <div data-testid="mission-control" data-bucket={filter}>
      <RunStatePills buckets={buckets} />
      <SystemRunsToggle show={showSystem} system={split.system} systemFailed={split.systemFailed} onChange={setShowSystem} />
      <MetricStrip metrics={metrics} />

      {shows("needs-you") && (
        <Section
          title={t("runs.mission.needsYou")}
          id="needs-you"
          count={buckets.needsYou.length}
          amber={buckets.needsYou.length > 0}
          testId="section-needs-you"
          hint={t("runs.mission.hint.needsYou")}
          alwaysShow
          emptyLine={t(EMPTY_LINE["needs-you"])}
        >
          <RowList runs={buckets.needsYou} />
        </Section>
      )}

      {shows("running") && (
        <Section
          title={t("runs.mission.running")}
          id="running"
          count={buckets.running.length}
          testId="section-running"
          // p0343b: the spine hint is honest — it only shows when the running
          // runs actually carry server-computed beats (pre-beats rows don't).
          hint={buckets.running.some((run) => run.beats) ? t("runs.mission.hint.running") : undefined}
          alwaysShow={filter === "running"}
          emptyLine={t(EMPTY_LINE.running)}
        >
          <RowList runs={buckets.running} />
        </Section>
      )}

      {shows("queued") && (
        <Section
          title={t("runs.mission.queued")}
          id="queued"
          count={buckets.queued.length}
          testId="section-queued"
          hint={t("runs.mission.hint.queued")}
          alwaysShow={filter === "queued"}
          emptyLine={t(EMPTY_LINE.queued)}
        >
          <RowList runs={buckets.queued} />
        </Section>
      )}

      {shows("finished") && (
        <Section
          title={t("runs.mission.finished")}
          id="finished"
          count={buckets.finished.length}
          testId="section-finished"
          action={buckets.finished.length > 0 ? <ClearTerminalRunsButton includeSystem={showSystem} /> : undefined}
          alwaysShow={filter === "finished" || (!exhausted && finishedLoaded > 0)}
          emptyLine={t(EMPTY_LINE.finished)}
          after={!exhausted && finishedLoaded > 0 && (
            <button
              type="button"
              className="load-more"
              data-testid="runs-load-more"
              onClick={loadMore}
              disabled={loadingMore}
            >
              {loadingMore ? t("runs.mission.loading") : t("runs.mission.loadMore")}
            </button>
          )}
        >
          <RowList runs={buckets.finished} />
        </Section>
      )}

      <footer>
        {t("runs.mission.footer")}
      </footer>
    </div>
  );
}

// 2026-08-25-39ab: one boundary per row. A run whose snapshot this build cannot
// render costs its own row a named error and nothing else — every other run in
// the bucket, and the rest of the home screen, keeps rendering.
function RowList({ runs }: { runs: Parameters<typeof RunRow>[0]["snapshot"][] }) {
  return (
    <div className="rows">
      {runs.map((run) => (
        <RenderBoundary key={run.runId} surface="shell.surface.run" surfaceParams={{ run: run.runId }}>
          <RunRow snapshot={run} />
        </RenderBoundary>
      ))}
    </div>
  );
}
