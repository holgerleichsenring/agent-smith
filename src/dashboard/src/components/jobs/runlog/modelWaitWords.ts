import type { T } from "@/lib/i18n/translate";
import type { ModelWait } from "./runNowLines";

// 2026-11-22-50d7a: a model wait in words — the status line's head on the ticket page and the run page, and the Your
// move box's title and sentence. The gate's own line (model, limit, config key) is shown as the server wrote it.

/** "19:37" — the wait's start in the reader's clock. */
export function waitClock(iso: string): string {
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return iso;
  return at.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false });
}

/** The status line's head: "Waiting for the model since 19:37"; null when the run is not waiting. */
export function modelWaitHead(t: T, wait: ModelWait | null): string | null {
  return wait ? t("run.modelWait.head", { at: waitClock(wait.since) }) : null;
}
