"use client";

import { use } from "react";
import { SystemView } from "@/components/system/SystemView";

// p0209b: /system is rail-driven master/detail. The optional-catch-all slug
// (set by the AppRail path-segment hrefs from p0209a) is the source of truth for
// the selected subsystem, so selection is URL-stable and survives refresh /
// deep-link by construction. The body lives in SystemView; this page only reads
// the slug and exports a default, satisfying Next's Page-type contract.

interface PageProps {
  params: Promise<{ slug?: string[] }>;
}

export default function SystemPage({ params }: PageProps) {
  const { slug } = use(params);
  return <SystemView segment={slug?.[0] ?? null} />;
}
