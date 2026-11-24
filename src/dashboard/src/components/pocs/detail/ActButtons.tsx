"use client";

import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { PocAct, PocDetail } from "@/lib/pocsApi";
import type { RunAct } from "./actRefusal";
import { DeleteDialog } from "./DeleteDialog";

// 2026-11-23-c81ed: Stop, Start, Apply and Delete — each enabled only when the server's answer admits it from the
// PoC's state for this caller. Each is a run that changes the desired state and commits it; Argo CD carries it out.
// Roll back sits on the revision it rolls back to.
// 2026-11-23-c81ee: the detail page holds whether the delete dialog is open, since the expiry banner opens it too.

const BUTTONS: { act: Exclude<PocAct, "rollback">; label: TextKey }[] = [
  { act: "stop", label: "pocs.detail.act.stop" },
  { act: "start", label: "pocs.detail.act.start" },
  { act: "apply", label: "pocs.detail.act.apply" },
  { act: "delete", label: "pocs.detail.act.delete" },
];

export function ActButtons({ poc, busy, onAct, deleting, setDeleting }: {
  poc: PocDetail; busy: boolean; onAct: RunAct; deleting: boolean; setDeleting: (open: boolean) => void;
}) {
  const t = useT();
  return (
    <section className="poc-box poc-acts" data-testid="poc-detail-acts">
      <h2>{t("pocs.detail.acts.title")}</h2>
      <div className="poc-bar">
        {BUTTONS.map(({ act, label }) => (
          <button key={act} type="button" className={act === "delete" ? "btn danger" : "btn"}
            disabled={busy || !poc.acts[act] || (act === "delete" && deleting)} data-testid={`poc-detail-act-${act}`}
            onClick={() => (act === "delete" ? setDeleting(true) : void onAct(act))}>
            {t(label)}
          </button>
        ))}
      </div>
      <p className="poc-meta">{poc.mayAct ? t("pocs.detail.acts.note") : t("pocs.detail.acts.mayNot")}</p>
      {deleting && poc.acts.delete && (
        <DeleteDialog name={poc.name} busy={busy} onAct={onAct} onClose={() => setDeleting(false)} />
      )}
    </section>
  );
}
