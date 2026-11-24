"use client";

import { useT } from "@/hooks/useT";

/** 2026-11-23-933da: the list before the first PoC — what will show here and how it gets here. */
export function PocEmpty() {
  const t = useT();
  return (
    <div className="poc-empty" data-testid="pocs-empty">
      <strong>{t("pocs.empty.title")}</strong>
      <span>{t("pocs.empty.body")}</span>
    </div>
  );
}
