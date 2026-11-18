"use client";

import { useState } from "react";
import { postFollowUp } from "@/lib/ticketsApi";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketSeriesSpec } from "@/types/tickets";

// 2026-11-18-c8e2d: the follow-up specs the masters proposed across the series — work each judged not worth holding
// its spec for. The person decides: "File as a ticket" files one as a work ticket through a 'file' run; a second click
// is refused by the server, so a filed one says so.

type Filing = { state: "filing" } | { state: "filed"; key: string } | { state: "refused"; text: string };

export function TicketFollowUps({ specs }: { specs: TicketSeriesSpec[] }) {
  const { project, ticketId } = useTicketDetail();
  const [filing, setFiling] = useState<Record<string, Filing>>({});
  const items = specs.flatMap((spec) => (spec.followUps ?? []).map((f) => ({ ...f, spec })));
  if (items.length === 0) return null;
  const file = async (key: string, runId: string, specId: string, index: number) => {
    setFiling((m) => ({ ...m, [key]: { state: "filing" } }));
    try {
      const filed = await postFollowUp(project, ticketId, { runId, specId, index });
      setFiling((m) => ({ ...m, [key]: { state: "filed", key: filed.key } }));
    } catch (e) {
      setFiling((m) => ({ ...m, [key]: { state: "refused", text: e instanceof Error ? e.message : String(e) } }));
    }
  };
  return (
    <div className="doc" data-testid="acceptance-follow-ups">
      <h5>Proposed follow-ups</h5>
      <div className="crit">
        {items.map((f) => {
          const key = `${f.runId}|${f.spec.specId}|${f.index}`;
          const now = filing[key];
          return (
            <div key={key} className="ci" data-testid="acceptance-follow-up">
              <span className="m q">+</span>
              <span>
                {f.goal}
                {f.why && <span className="tk-crit-why">{f.why}</span>}
                {now?.state === "refused" && <span className="dim-line"> · {now.text}</span>}
              </span>
              <span className="cw">
                Spec {f.spec.ordinal}{" "}
                {now?.state === "filed" ? <span data-testid="follow-up-filed">filed as {now.key}</span> : (
                  <button type="button" className="btn sm" disabled={now?.state === "filing"}
                    onClick={() => void file(key, f.runId, f.spec.specId, f.index)} data-testid="follow-up-file">
                    File as a ticket
                  </button>
                )}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
