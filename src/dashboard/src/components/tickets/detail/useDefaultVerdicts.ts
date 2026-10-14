"use client";

import { useState } from "react";
import { postDefaultVerdicts } from "@/lib/ticketsApi";
import type { DefaultVerdictAnswer, DefaultVerdictItem } from "@/types/tickets";

// 2026-10-13-8762q: the Defaults tab's one act — the selected pending defaults ratified or overruled together,
// one note going with every overrule of the act; the store re-reads the defaults after every answer.

export type VerdictOutcome =
  | { kind: "idle" }
  | { kind: "sending" }
  | { kind: "answered"; answer: DefaultVerdictAnswer }
  | { kind: "failed"; text: string };

export function useDefaultVerdicts(project: string, ticketId: string, changed: () => void) {
  const [selected, setSelected] = useState<ReadonlySet<number>>(new Set());
  const [note, setNote] = useState("");
  const [outcome, setOutcome] = useState<VerdictOutcome>({ kind: "idle" });

  const toggle = (id: number) => setSelected((held) => {
    const next = new Set(held);
    if (!next.delete(id)) next.add(id);
    return next;
  });

  const send = async (verdict: DefaultVerdictItem["verdict"]) => {
    const trimmed = note.trim();
    const items: DefaultVerdictItem[] = [...selected].map((id) =>
      verdict === "overruled" && trimmed ? { id, verdict, note: trimmed } : { id, verdict });
    setOutcome({ kind: "sending" });
    try {
      const answer = await postDefaultVerdicts(project, ticketId, items);
      setOutcome({ kind: "answered", answer });
      if (answer.kind === "applied") {
        setSelected(new Set());
        setNote("");
      }
    } catch (thrown) {
      setOutcome({ kind: "failed", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
    changed();
  };

  return { selected, setSelected, toggle, note, setNote, outcome, send };
}
