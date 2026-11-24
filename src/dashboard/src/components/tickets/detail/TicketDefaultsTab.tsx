"use client";

import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { useTicketDefaults, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketDefault } from "@/types/tickets";
import { DefaultRow, verdictWords } from "./DefaultRow";
import { RUNS_CONTROL } from "./RetryUnchanged";
import { useDefaultVerdicts, type VerdictOutcome } from "./useDefaultVerdicts";

// 2026-10-13-8762q: the ticket's defaults — choices a master took without asking, which a person ratifies or
// overrules later. Pending first, in the server's order (those holding the series on top), then decided. A
// design conversation's default is ratify-only here; it is overruled in that conversation. The bar and the
// checkboxes are hidden only from a caller KNOWN to lack runs.control: the server decides for everyone else.

/** 2026-11-23-5669b: the hint's dictionary key. */
export const ANSWER_IN_CONVERSATION: TextKey = "tickets.defaults.answerInConversation";

export function TicketDefaultsTab() {
  const t = useT();
  const { project, ticketId } = useTicketDetail();
  const defaults = useTicketDefaults();
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const act = useDefaultVerdicts(project, ticketId, defaults.changed);
  const entries = defaults.data?.entries ?? [];
  const pending = entries.filter((e) => e.verdict === "pending");
  const decided = entries.filter((e) => e.verdict !== "pending");
  const chosen = pending.filter((e) => act.selected.has(e.id));
  const holdsConversation = chosen.some((e) => e.conversation !== null);
  const sending = act.outcome.kind === "sending";
  const all = pending.length > 0 && chosen.length === pending.length;

  return (
    <>
      <div className="section-head first">
        <h2>{t("tickets.defaults.pending")}</h2>
        <span className="cnt" data-testid="defaults-pending-count">{defaults.data?.pending ?? 0}</span>
        <span className="sh-sub">{t("tickets.defaults.sub")}</span>
      </div>
      {entries.length === 0 && (
        <div className="msub">{defaults.loading ? t("tickets.defaults.reading") : t("tickets.defaults.none")}</div>
      )}
      {pending.length > 0 && !lacks && (
        <div className="df-bar" data-testid="defaults-bar">
          <label className="df-all">
            <input type="checkbox" checked={all} data-testid="defaults-select-all"
              onChange={() => act.setSelected(new Set(all ? [] : pending.map((e) => e.id)))} />
            {t("tickets.defaults.selectAll")}
          </label>
          <input type="text" className="df-note-in" value={act.note} onChange={(e) => act.setNote(e.target.value)}
            placeholder={t("tickets.defaults.notePlaceholder")} aria-label={t("tickets.defaults.noteLabel")} data-testid="defaults-note" />
          <button type="button" className="btn sm primary" disabled={chosen.length === 0 || sending}
            onClick={() => void act.send("ratified")} data-testid="defaults-ratify">
            {t("tickets.defaults.ratify")}
          </button>
          <button type="button" className="btn sm" disabled={chosen.length === 0 || sending || holdsConversation}
            onClick={() => void act.send("overruled")} data-testid="defaults-overrule">
            {t("tickets.defaults.overrule")}
          </button>
          {holdsConversation && <span className="cp-note" data-testid="defaults-conversation-hint">{t(ANSWER_IN_CONVERSATION)}</span>}
        </div>
      )}
      <Outcome outcome={act.outcome} entries={entries} />
      <div className="df-list" data-testid="defaults-pending">
        {pending.map((entry) => (
          <DefaultRow key={entry.id} entry={entry} selected={lacks ? null : act.selected.has(entry.id)} onToggle={act.toggle} />
        ))}
      </div>
      {decided.length > 0 && (
        <>
          <div className="section-head">
            <h2>{t("tickets.defaults.decided")}</h2>
            <span className="cnt">{decided.length}</span>
          </div>
          <div className="df-list" data-testid="defaults-decided">
            {decided.map((entry) => <DefaultRow key={entry.id} entry={entry} selected={null} />)}
          </div>
        </>
      )}
    </>
  );
}

function Outcome({ outcome, entries }: { outcome: VerdictOutcome; entries: TicketDefault[] }) {
  const t = useT();
  if (outcome.kind === "failed") return <div className="cp-note bad df-out" data-testid="defaults-refused">{outcome.text}</div>;
  if (outcome.kind !== "answered") return null;
  const answer = outcome.answer;
  if (answer.kind === "refused") return <div className="cp-note bad df-out" data-testid="defaults-refused">{answer.text}</div>;
  const named = (id: number) => entries.find((e) => e.id === id)?.question ?? t("tickets.defaults.numbered", { id });
  return (
    <div className="cp-note df-out" data-testid="defaults-outcome">
      {t("tickets.defaults.applied", { count: answer.applied.length })}
      {answer.alreadyDecided.map((standing) => (
        <div key={standing.id} data-testid="defaults-already-decided">
          {t("tickets.defaults.already", { question: named(standing.id), verdict: verdictWords(t, standing.verdict) })}
        </div>
      ))}
    </div>
  );
}
