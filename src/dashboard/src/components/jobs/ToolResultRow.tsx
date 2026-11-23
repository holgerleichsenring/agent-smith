"use client";

import { useT } from "@/hooks/useT";

import type { ToolResultEvent } from "@/types/hub-events";

interface Props {
  event: ToolResultEvent;
}

export function ToolResultRow({ event }: Props) {
  const t = useT();
  const okClass = event.ok ? "text-app-proven" : "text-app-stopped";
  return (
    <div className="flex items-center gap-2 text-xs text-app-text-soft" data-testid="tool-result-row">
      <span className="text-app-mute">←</span>
      <span className="font-mono text-app-needs">{event.tool}</span>
      <span className={okClass}>{event.ok ? t("runs.tools.ok") : t("runs.tools.fail")}</span>
      <span className="text-app-mute">{t("runs.tools.resultBytes", { bytes: event.resultLength })}</span>
    </div>
  );
}
