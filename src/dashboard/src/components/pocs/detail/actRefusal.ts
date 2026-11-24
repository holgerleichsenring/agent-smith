import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import type { T } from "@/lib/i18n/translate";
import type { PocAct, PocActBody } from "@/lib/pocsApi";

// 2026-11-23-c81ed: what a refused act or variable write reads as — the server's sentence as it came; a refusal that
// said nothing gets one plain line instead.

/** Start one act's run; true once the run was queued. The detail page holds busy, the run and the refusal. */
export type RunAct = (act: PocAct, body?: PocActBody) => Promise<boolean>;

export function actRefusal(t: T, err: unknown): string {
  if (err instanceof ApiRefusal) return t("pocs.detail.refusal.forbidden");
  if (err instanceof ApiResponseError) {
    if (err.reason) return err.reason;
    return err.status === 404 ? t("pocs.detail.notFound") : t("pocs.detail.refusal.unsaid", { status: err.status });
  }
  return t("pocs.detail.refusal.failed", { message: err instanceof Error ? err.message : String(err) });
}
