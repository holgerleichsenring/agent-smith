"use client";

import { useLacksPermission } from "@/hooks/useLacksPermission";
import { usePocsFeature, type PocsStatus } from "@/hooks/usePocsFeature";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { PocList } from "./PocList";
import "@/styles/pocs.css";

// 2026-11-23-933da: /pocs — the list when the feature is on and readable, else one plain sentence saying why not.
// 'Target settings' shows unless the caller is known to lack config.read.

export const CONFIG_READ = "config.read";

/** Why the PoCs area shows no list: off, not permitted, failed, or still loading. The import page says it the same way. */
export function pocsUnavailable(t: T, status: PocsStatus, failed: boolean): string {
  return status === "off" ? t("pocs.off")
    : status === "forbidden" ? t("pocs.forbidden")
    : failed ? t("pocs.failed") : t("pocs.loading");
}

export function PocsHome() {
  const t = useT();
  const { status, list, failed } = usePocsFeature();
  const lacksConfig = useLacksPermission(CONFIG_READ);
  if (status === "on" && list) return <PocList list={list} showSettings={!lacksConfig} />;
  const sentence = pocsUnavailable(t, status, failed);
  return (
    <>
      <div className="m-head"><div><h1>{t("pocs.title")}</h1></div></div>
      <div className="msub note" data-testid={`pocs-${failed ? "failed" : status}`}>{sentence}</div>
    </>
  );
}
