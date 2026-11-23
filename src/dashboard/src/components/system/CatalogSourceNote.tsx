"use client";

import { useT } from "@/hooks/useT";

// 2026-09-18-84be: the ONE composition of "where a change to this text is made". The
// origin line and every expanded SKILL.md render this same component, so the page cannot
// say it two ways — and the overlay case is a different sentence, not a hedge on the
// source one: on an overlaid installation the body may be the operator's own file, which
// the Skills settings form does not carry and therefore cannot change.

export function CatalogSourceNote({
  overlayPath,
  testId,
}: {
  overlayPath: string | null;
  testId: string;
}) {
  const t = useT();
  if (overlayPath !== null) {
    // The path sits inside the sentence in mono, so the text is cut at its placeholder.
    const [before, after] = t("system.catalog.overlay").split("{path}");
    return (
      <span className="msub" data-testid={testId}>
        {before}
        <span className="mono">{overlayPath}</span>
        {after}
      </span>
    );
  }
  return (
    <span className="msub" data-testid={testId}>
      {t("system.catalog.source")}
    </span>
  );
}
