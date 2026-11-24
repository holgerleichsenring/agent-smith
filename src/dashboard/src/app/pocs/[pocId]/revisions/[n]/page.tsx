"use client";

import { use } from "react";
import { PocRevisionPage } from "@/components/pocs/blocked/PocRevisionPage";

// 2026-11-23-933dc: one revision of a PoC — why the scan blocked it, or what it noted — in the PoCs list's frame.

interface PageProps {
  params: Promise<{ pocId: string; n: string }>;
}

export default function PocRevisionRoute({ params }: PageProps) {
  const { pocId, n } = use(params);
  const id = Number(decodeURIComponent(pocId));
  const revision = Number(decodeURIComponent(n));
  return (
    <div className="mock-shell mock-tickets mock-pocs" data-testid="poc-revision-page">
      <main className="main">
        <PocRevisionPage key={`${id}/${revision}`} pocId={id} revision={revision} />
      </main>
    </div>
  );
}
