"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import type { TicketDefault } from "@/types/tickets";
import { ago } from "../ticketFormat";

// 2026-10-13-8762q: one default on the Defaults tab — where it was taken, what was asked and chosen, and for a
// decided one who decided it, when and with which note. 2026-10-25-6c1f: the memory-proposal pointer is gone;
// the defaults read stopped serving those rows in 2026-10-25-9324e.

interface Props {
  entry: TicketDefault;
  /** Null: no checkbox (a decided row, or a caller known to lack runs.control). */
  selected: boolean | null;
  onToggle?: (id: number) => void;
}

export function DefaultRow({ entry, selected, onToggle }: Props) {
  const t = useT();
  const pending = entry.verdict === "pending";
  const held = pending && entry.holdsSeries;
  const taken = ago(t, entry.takenAt);
  return (
    <div className={held ? "df-row held" : "df-row"} data-testid={`default-${entry.id}`}>
      {selected !== null ? (
        <input type="checkbox" checked={selected} onChange={() => onToggle?.(entry.id)}
          aria-label={t("tickets.defaults.select", { id: entry.id })} data-testid={`default-select-${entry.id}`} />
      ) : <span />}
      <div className="df-main">
        <div className="df-meta">
          <Origin t={t} entry={entry} />
          {entry.specId && <span>{t("tickets.conversation.specId", { id: entry.specId })}</span>}
          {taken && <span>{taken}</span>}
          {held && <span className="df-held" data-testid="default-holds">{t("tickets.defaults.holdsSeries")}</span>}
        </div>
        {entry.question && <div className="df-q">{entry.question}</div>}
        <Body t={t} entry={entry} />
        {!pending && <Verdict t={t} entry={entry} />}
      </div>
    </div>
  );
}

function Origin({ t, entry }: { t: T; entry: TicketDefault }) {
  if (entry.conversation) return <span data-testid="default-origin">{t("tickets.defaults.inConversation")}</span>;
  if (entry.attempt !== null) return <span data-testid="default-origin">{t("tickets.defaults.attempt", { n: entry.attempt })}</span>;
  return <span className="mono" data-testid="default-origin">{t("tickets.defaults.run", { id: entry.runId })}</span>;
}

function Body({ t, entry }: { t: T; entry: TicketDefault }) {
  if (entry.chosen === null && entry.why === null) return null;
  const alternatives = entry.alternatives ?? [];
  return (
    <dl className="df-body" data-testid="default-body">
      {entry.chosen !== null && <><dt>{t("tickets.defaults.chosen")}</dt><dd>{entry.chosen}</dd></>}
      {alternatives.length > 0 && <><dt>{t("tickets.defaults.insteadOf")}</dt><dd>{alternatives.join(" · ")}</dd></>}
      {entry.why !== null && <><dt>{t("tickets.defaults.why")}</dt><dd>{entry.why}</dd></>}
    </dl>
  );
}

function Verdict({ t, entry }: { t: T; entry: TicketDefault }) {
  const when = ago(t, entry.verdictAt);
  return (
    <div className="df-verdict" data-testid="default-verdict">
      <span className={entry.verdict === "ratified" ? "pill ok" : "pill bad"}>{verdictWords(t, entry.verdict)}</span>
      {entry.verdictBy && <span>{t("tickets.defaults.by", { by: entry.verdictBy })}</span>}
      {when && <span>{when}</span>}
      {entry.verdictNote && <span className="df-note">{t("common.quoted", { text: entry.verdictNote })}</span>}
    </div>
  );
}

/** A default's verdict in words; an unknown one as the server spelled it. */
export function verdictWords(t: T, verdict: string): string {
  return verdict === "ratified" ? t("tickets.defaults.ratified") : verdict === "overruled" ? t("tickets.defaults.overruled")
    : verdict === "pending" ? t("tickets.defaults.pendingWord") : verdict;
}
