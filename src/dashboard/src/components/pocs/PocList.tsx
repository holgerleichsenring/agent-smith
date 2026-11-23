"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import type { PocList as PocListAnswer } from "@/lib/pocsApi";
import { cn } from "@/lib/utils";
import { PocCard } from "./PocCard";
import { PocEmpty } from "./PocEmpty";
import { PocStateLabel } from "./PocStateLabel";
import { PocTargetStrip } from "./PocTargetStrip";
import { pocDot, statesPresent } from "./pocStates";

// 2026-11-23-933da: the PoCs a caller may see, as cards, with a pill per state present to narrow them, and the target
// strip below. Import ZIP is shown to whoever may import and stays disabled until import arrives (2026-11-23-933db).

const ALL = "";

export function PocList({ list, showSettings }: { list: PocListAnswer; showSettings: boolean }) {
  const t = useT();
  const [state, setState] = useState(ALL);
  const states = statesPresent(list.pocs.map((poc) => poc.state));
  const shown = state === ALL ? list.pocs : list.pocs.filter((poc) => poc.state === state);
  const count = (s: string) => list.pocs.filter((poc) => poc.state === s).length;
  return (
    <>
      <div className="m-head">
        <div>
          <h1>{t("pocs.title")}</h1>
          <div className="msub">{t("pocs.sub")}</div>
        </div>
        {list.mayImport && (
          <button type="button" className="btn primary poc-import" disabled title={t("pocs.importLater")}
            data-testid="pocs-import">
            {t("pocs.import")}
          </button>
        )}
      </div>
      {list.pocs.length > 0 && (
        <div className="poc-pills" role="group" aria-label={t("pocs.filter.label")} data-testid="pocs-pills">
          <Pill on={state === ALL} onClick={() => setState(ALL)} testId="pocs-pill-all">
            {t("pocs.filter.all")} <b>{list.pocs.length}</b>
          </Pill>
          {states.map((s) => (
            <Pill key={s} on={state === s} onClick={() => setState(s)} testId={`pocs-pill-${s}`}>
              <span className={`poc-dot ${pocDot(s)}`} aria-hidden="true" /><PocStateLabel state={s} /> <b>{count(s)}</b>
            </Pill>
          ))}
        </div>
      )}
      {list.pocs.length === 0 && <PocEmpty />}
      {list.pocs.length > 0 && shown.length === 0 && <div className="poc-empty">{t("pocs.filtered.empty")}</div>}
      {shown.length > 0 && (
        <div className="poc-grid" data-testid="pocs-grid">
          {shown.map((poc) => <PocCard key={poc.id} poc={poc} />)}
        </div>
      )}
      <PocTargetStrip targets={list.targets} showSettings={showSettings} />
    </>
  );
}

function Pill({ on, onClick, testId, children }: { on: boolean; onClick: () => void; testId: string; children: React.ReactNode }) {
  return (
    <button type="button" className={cn("poc-pill", on && "on")} aria-pressed={on} onClick={onClick} data-testid={testId}>
      {children}
    </button>
  );
}
