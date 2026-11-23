"use client";

import { Suspense, use } from "react";
import { SeriesRunPage } from "@/components/series/page/SeriesRunPage";

// 2026-11-23-31dcd: one series. ?section= is read under this Suspense boundary, never in the root layout.

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function SeriesRunRoute({ params }: PageProps) {
  const { id } = use(params);
  const decoded = decodeURIComponent(id);
  return (
    <Suspense fallback={null}>
      <SeriesRunPage key={decoded} id={decoded} />
    </Suspense>
  );
}
