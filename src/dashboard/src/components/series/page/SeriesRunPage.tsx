"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useT } from "@/hooks/useT";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import "@/styles/ticket-thread.css";
import "@/styles/series.css";
import { SERIES_SECTION_PARAM, seriesSectionOf, seriesSections } from "./seriesPageSections";
import { SeriesSectionBar } from "./SeriesSectionBar";
import { useSeriesRunDetail } from "./useSeriesRunDetail";

// 2026-11-23-31dcd: one series, built like the ticket page — its bar beside the icon-only sidebar and ONE section
// beside it, the one ?section= names (the first listed one without it). Selecting a section rewrites the address.

export function SeriesRunPage({ id }: { id: string }) {
  const { read, reload } = useSeriesRunDetail(id);
  const t = useT();
  if (read.kind !== "read") {
    return (
      <div className="mock-shell mock-tickets" data-testid="series-page">
        <main className="main tk-page">
          <div className="msub" data-testid="series-page-unread">
            {read.kind === "loading" ? t("series.page.loading") : read.kind === "notFound" ? t("series.page.notFound") : t("series.page.failed")}
            {read.kind !== "loading" && <> <Link href={SERIES_PATH}>{t("series.page.backToList")}</Link></>}
          </div>
        </main>
      </div>
    );
  }
  return <SeriesSections detail={read.detail} reload={reload} />;
}

function SeriesSections({ detail, reload }: { detail: SeriesRunDetail; reload: () => void }) {
  const t = useT();
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname() ?? SERIES_PATH;
  const section = seriesSectionOf(params?.get(SERIES_SECTION_PARAM) ?? null);
  const select = (next: string) => {
    const query = new URLSearchParams(params?.toString() ?? "");
    if (next === seriesSections[0].id) query.delete(SERIES_SECTION_PARAM);
    else query.set(SERIES_SECTION_PARAM, next);
    const search = query.toString();
    router.replace(search ? `${pathname}?${search}` : pathname, { scroll: false });
  };
  return (
    <div className="mock-shell mock-tickets tk-shell" data-testid="series-page">
      <SeriesSectionBar detail={detail} current={section} onSelect={select} />
      <main className="main tk-page" data-section={section.id}>
        <div className="tk-heading"><h1 data-testid="series-section-title">{t(section.label)}</h1></div>
        <section className="tk-pane" data-testid={`series-pane-${section.id}`}>
          <section.Pane detail={detail} reload={reload} />
        </section>
      </main>
    </div>
  );
}
