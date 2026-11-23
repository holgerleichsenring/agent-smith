"use client";

import { useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import { fetchReadableProjects } from "@/lib/readableProjectsApi";
import { fetchSeriesRuns } from "@/lib/seriesRunsApi";
import type { SeriesRunRow } from "@/types/seriesRuns";
import { SeriesRunListRow } from "./SeriesRunListRow";
import { useSeriesProjectFilter } from "./useSeriesProjectFilter";
import "@/styles/series.css";

// 2026-11-23-31dcc: /series — every series the caller may see, newest activity first as the server orders them, one
// row each, narrowed to one project by a filter this browser remembers.

type Read = { kind: "loading" } | { kind: "failed"; message: string } | { kind: "read"; rows: SeriesRunRow[] };

export function SeriesRunsHome() {
  const t = useT();
  const [project, setProject, ready] = useSeriesProjectFilter();
  const [projects, setProjects] = useState<string[]>([]);
  const [read, setRead] = useState<Read>({ kind: "loading" });
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
      </div>
      <SeriesRunsBody read={read} />
    </>
  );
}

function SeriesRunsBody({ read }: { read: Read }) {
  const t = useT();
  if (read.kind === "loading") return <div className="msub note" data-testid="series-loading">{t("series.list.loading")}</div>;
  if (read.kind === "failed") return <div className="msub note" data-testid="series-failed">{t("series.list.failed", { message: read.message })}</div>;
  if (read.rows.length === 0) return <div className="sr-empty" data-testid="series-empty">{t("series.list.empty")}</div>;
  return <div className="rows" data-testid="series-rows">{read.rows.map((row) => <SeriesRunListRow key={row.id} row={row} />)}</div>;
}
