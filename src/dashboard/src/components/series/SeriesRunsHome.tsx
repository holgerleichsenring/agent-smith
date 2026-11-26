"use client";

import { useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import { fetchReadableProjects } from "@/lib/readableProjectsApi";
import { fetchSeriesRuns } from "@/lib/seriesRunsApi";
import type { SeriesRunRow } from "@/types/seriesRuns";
import { SeriesRunListRow } from "./SeriesRunListRow";
import { seriesEnded } from "./seriesNextStep";
import { useSeriesProjectFilter } from "./useSeriesProjectFilter";
import "@/styles/series.css";

// 2026-11-23-31dcc: /series — every series the caller may see, newest activity first as the server orders them, one
// row each, narrowed to one project by a filter this browser remembers.
// 2026-11-26-5201d: ended series (done, stopped) are hidden until "Show ended" is ticked; the list says how many it hides.
// A view filter only: nothing is deleted, the stop record stays.

type Read = { kind: "loading" } | { kind: "failed"; message: string } | { kind: "read"; rows: SeriesRunRow[] };

export function SeriesRunsHome() {
  const t = useT();
  const [project, setProject, ready] = useSeriesProjectFilter();
  const [projects, setProjects] = useState<string[]>([]);
  const [read, setRead] = useState<Read>({ kind: "loading" });
  const [showEnded, setShowEnded] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    fetchReadableProjects(controller.signal).then(setProjects).catch(() => {});
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (!ready) return;
    const controller = new AbortController();
    setRead({ kind: "loading" });
    fetchSeriesRuns(project || null, controller.signal)
      .then((rows) => { if (!controller.signal.aborted) setRead({ kind: "read", rows }); })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setRead({ kind: "failed", message: error instanceof Error ? error.message : String(error) });
      });
    return () => controller.abort();
  }, [project, ready]);
  return (
    <>
      <div className="m-head">
        <div>
          <h1>{t("series.list.title")}</h1>
          <div className="msub">{t("series.list.sub")}</div>
        </div>
        <label className="sr-filter">
          <span>{t("series.list.project")}</span>
          <select value={project} onChange={(event) => setProject(event.target.value)} data-testid="series-project">
            <option value="">{t("series.list.allProjects")}</option>
            {[...new Set([...projects, ...(project ? [project] : [])])].map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
        </label>
        <label className="sr-filter sr-ended-toggle">
          <input type="checkbox" checked={showEnded} onChange={(event) => setShowEnded(event.target.checked)} data-testid="series-show-ended" />
          <span>{t("series.list.showEnded")}</span>
        </label>
      </div>
      <SeriesRunsBody read={read} showEnded={showEnded} />
    </>
  );
}

function SeriesRunsBody({ read, showEnded }: { read: Read; showEnded: boolean }) {
  const t = useT();
  if (read.kind === "loading") return <div className="msub note" data-testid="series-loading">{t("series.list.loading")}</div>;
  if (read.kind === "failed") return <div className="msub note" data-testid="series-failed">{t("series.list.failed", { message: read.message })}</div>;
  if (read.rows.length === 0) return <div className="sr-empty" data-testid="series-empty">{t("series.list.empty")}</div>;
  const shown = showEnded ? read.rows : read.rows.filter((row) => !seriesEnded(row.state));
  const hidden = read.rows.length - shown.length;
  if (shown.length === 0) return <div className="sr-empty" data-testid="series-empty">{t("series.list.allEnded", { count: hidden })}</div>;
  return (
    <>
      <div className="rows" data-testid="series-rows">{shown.map((row) => <SeriesRunListRow key={row.id} row={row} />)}</div>
      {hidden > 0 && <div className="msub sr-hidden" data-testid="series-hidden">{t("series.list.hiddenEnded", { count: hidden })}</div>}
    </>
  );
}
