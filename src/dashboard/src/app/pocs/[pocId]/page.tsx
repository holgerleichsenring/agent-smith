"use client";

import { use } from "react";
import { PocDetailPage } from "@/components/pocs/detail/PocDetailPage";

// 2026-11-23-c81ed: one PoC — its revisions, its acts, its variables and its audit — in the PoCs list's frame.

interface PageProps {
  params: Promise<{ pocId: string }>;
}

export default function PocDetailRoute({ params }: PageProps) {
  const { pocId } = use(params);
  const id = Number(decodeURIComponent(pocId));
  return (
    <div className="mock-shell mock-tickets mock-pocs" data-testid="poc-detail-page">
      <main className="main">
        <PocDetailPage key={id} pocId={id} />
      </main>
    </div>
  );
}
