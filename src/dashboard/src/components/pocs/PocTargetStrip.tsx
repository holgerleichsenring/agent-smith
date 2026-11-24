"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { PocTarget } from "@/lib/pocsApi";

// 2026-11-23-933da: where a PoC's source lives — per target its name, the source repository and the folder, from the
// list answer itself, so a PoC-only caller without config.read sees it too. The settings link is for config readers.

export const POCS_SETTINGS_PATH = "/config/settings/pocs";

export function PocTargetStrip({ targets, showSettings }: { targets: PocTarget[]; showSettings: boolean }) {
  const t = useT();
  return (
    <div className="poc-targets" data-testid="pocs-targets">
      {targets.length === 0 && <span>{t("pocs.target.none")}</span>}
      {targets.map((target) => (
        <div className="poc-target" key={target.name} data-testid={`pocs-target-${target.name}`}>
          <span>{t("pocs.target.label")} <b>{target.name}</b></span>
          <span>{t("pocs.target.source")} <b className="mono">{target.sourceRepo}</b></span>
          <span>{t("pocs.target.folder")} <b className="mono">{target.root}/</b></span>
        </div>
      ))}
      {showSettings && (
        <Link className="poc-target-settings" href={POCS_SETTINGS_PATH} data-testid="pocs-target-settings">
          {t("pocs.target.settings")} →
        </Link>
      )}
    </div>
  );
}
