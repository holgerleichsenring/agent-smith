"use client";

import { useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { TrackerText } from "@/components/ui/TrackerText";
import { useDateLocale, useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { useTicketText } from "@/lib/tickets/TicketPageStore";
import type { TicketText, TicketTextReason } from "@/types/tickets";
import { clock } from "./flowWords";

// The ticket itself at the top of the Summary tab: its type, its state now, labels and assignee, the
// description as its tracker wrote it, and the tracker's own acceptance criteria — labelled apart from the
// Acceptance tab, which lists the specs' done criteria. A snapshot says which attempt read it and when.

const REASON_WORDS: Readonly<Record<TicketTextReason, TextKey>> = {
  "not-found": "tickets.text.reason.not-found",
  unauthorized: "tickets.text.reason.unauthorized",
  unreachable: "tickets.text.reason.unreachable",
  "project-not-configured": "tickets.text.reason.project-not-configured",
};

export function TicketTextSection() {
  const t = useT();
  const read = useTicketText();
  const text = read.data;
  if (!text) {
    if (!read.failed) return null;
    return <section className="tt" data-testid="ticket-text"><div className="tt-note">{t("tickets.text.unread")}</div></section>;
  }
  return (
    <section className="tt" data-testid="ticket-text">
      <div className="tt-h">
        <span className="k">{t("tickets.thread.theTicket")}</span>
        <Chips text={text} />
      </div>
      {text.source === "unavailable" ? (
        <div className="tt-note" data-testid="ticket-text-unavailable">
          {t("tickets.text.unavailable", { reason: t(text.reason ? REASON_WORDS[text.reason] : REASON_WORDS.unreachable) })}
        </div>
      ) : (
        <Body text={text} />
      )}
    </section>
  );
}

function Chips({ text }: { text: TicketText }) {
  const t = useT();
  return (
    <span className="tt-chips">
      {text.workItemType && <span className="tchip" data-testid="ticket-text-type">{text.workItemType}</span>}
      {text.state && <span className="tchip" data-testid="ticket-text-state">{t("tickets.text.state", { state: text.state })}</span>}
      {(text.labels ?? []).map((label) => <span key={label} className="tchip label">{label}</span>)}
      {text.assignee && <span className="tt-who" data-testid="ticket-text-assignee">{t("tickets.text.assignee", { assignee: text.assignee })}</span>}
    </span>
  );
}

function Body({ text }: { text: TicketText }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <>
      {text.source === "snapshot" && (
        <div className="tt-note" data-testid="ticket-text-snapshot">
          {t("tickets.text.snapshot", { n: text.attempt ?? "", at: clock(text.at, locale) ?? "" })}
        </div>
      )}
      {text.description
        ? <Collapsed source={text.description}><TrackerText text={text.description} format={text.descriptionFormat} /></Collapsed>
        : <div className="tt-note">{t("tickets.text.noDescription")}</div>}
      {text.acceptanceCriteria && (
        <div className="tt-ac" data-testid="ticket-text-criteria">
          <span className="k">{t("tickets.text.criteria")}</span>
          <div className="tt-body"><TrackerText text={text.acceptanceCriteria} format={text.criteriaFormat} /></div>
        </div>
      )}
    </>
  );
}

/** Twelve lines, and "Show all" when the text runs longer. */
function Collapsed({ source, children }: { source: string; children: ReactNode }) {
  const t = useT();
  const box = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [overflows, setOverflows] = useState(false);
  useLayoutEffect(() => {
    const el = box.current;
    if (el) setOverflows(el.scrollHeight > el.clientHeight + 1);
  }, [source]);
  return (
    <>
      <div ref={box} className={open ? "tt-body" : "tt-body clamp"} data-testid="ticket-text-description">{children}</div>
      {overflows && !open && (
        <button type="button" className="tt-more" onClick={() => setOpen(true)}>{t("tickets.text.showAll")}</button>
      )}
    </>
  );
}
