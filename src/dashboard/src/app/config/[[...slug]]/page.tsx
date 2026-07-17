import { use } from "react";
import { ConfigStudio, type StudioSection } from "@/components/config/ConfigStudio";
import { isConfigEntityKind } from "@/components/config/entities";

// p0345: the Configuration studio route. The optional catch-all slug selects the
// section — /config → agents (default), /config/{kind} → that catalog,
// /config/changes → the audit view — so selection is URL-stable and deep-linkable,
// mirroring the /system route's slug-driven master/detail.

interface PageProps {
  params: Promise<{ slug?: string[] }>;
}

function sectionFromSlug(slug?: string[]): StudioSection {
  const seg = slug?.[0];
  if (seg === "changes") return "changes";
  if (isConfigEntityKind(seg)) return seg;
  return "agents";
}

export default function ConfigPage({ params }: PageProps) {
  const { slug } = use(params);
  return <ConfigStudio section={sectionFromSlug(slug)} />;
}
