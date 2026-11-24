import { dateLocale } from "@/lib/i18n/dates";
import { LANGUAGES, translator, type T } from "@/lib/i18n/translate";
import type { EndDetail } from "@/types/hub-events";
import type { TicketAttempt, TicketCounts, TicketHold, TicketSummary } from "@/types/tickets";
import { RUN_END_KINDS } from "@/types/tickets";
import { isAsked } from "./holdRoutes";

// 2026-11-23-31dcg: the words of a hold. The server reads the hold — its addressee, its case, its one act — and every
// surface (Your move on home and in the sidebar, the ticket's Your move box, the series' Overview) renders it
// through these functions, so a ticket says the same sentence everywhere and nothing here decides who acts. The end
// detail a run recorded is quoted where it names the cause.

/** 2026-11-23-5669b: the locale of the language a lookup speaks — each language has one lookup, so it names the language. */
function localeOf(t: T): string {
  return dateLocale(LANGUAGES.find((language) => translator(language) === t) ?? "en");
}

/** "#1", "#1 and #2", "#1, #2 and #3". */
export function namesOf(t: T, names: readonly string[]): string {
  if (names.length <= 1) return names.join("");
  return t("hold.and", { first: names.slice(0, -1).join(", "), last: names[names.length - 1] });
}

/** A run's recorded end detail as one clause — what the refusal is about and the setting that lifts it; null without one. */
export function toldOf(t: T, head: TicketAttempt | null | undefined): string | null {
  return told(head?.endKind ?? null, head?.endDetail ?? null, t);
}

// The subject, the repositories and the setting are the recorded words; the clause around them is the viewer's language.
function told(endKind: string | null, d: EndDetail | null, t: T): string | null {
  if (!d) return null;
  const set = d.control && d.setTo ? { control: d.control, setTo: d.setTo } : null;
  const lever = set ? t("hold.told.lever", set) : "";
  switch (endKind) {
    case RUN_END_KINDS.imageUntrusted: return t("hold.told.image", { subject: d.subject, lever });
    case RUN_END_KINDS.bootstrapMissing:
      return t("hold.told.bootstrap", { repos: d.repos.join(", "), lift: set ? t("hold.told.lift", set) : "", count: d.repos.length });
    default: return lever ? t("hold.told.subject", { subject: d.subject, lever }) : null;
  }
}

const KIND_OF_CASE: Partial<Record<TicketHold["case"], string>> = {
  image_untrusted: RUN_END_KINDS.imageUntrusted, bootstrap_missing: RUN_END_KINDS.bootstrapMissing, preflight: RUN_END_KINDS.preflight,
};

/** 2026-11-23-31dci: "00:12" for a wait that began today, "21 Nov 00:12" for an older one — in the reader's clock and,
 *  2026-11-23-5669b, the chosen language's locale. */
export function sinceClock(iso: string, locale: string, now: Date = new Date()): string {
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return iso;
  const time = at.toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit", hour12: false });
  if (at.toDateString() === now.toDateString()) return time;
  return `${at.toLocaleDateString(locale, { day: "numeric", month: "short" })} ${time}`;
}

// 2026-11-23-31dci: the waits that say since when; a wait without a since keeps its plain sentence.
const WAITS = new Set<TicketHold["case"]>(["model_wait", "queued", "resuming", "restarting", "prerequisite", "ci_running", "refreshing"]);

/** 2026-11-23-31dci: a wait's clause with its since — "Waiting for the model since 00:12"; null when the hold is no wait
 *  or carries no since. */
export function waitClause(t: T, hold: TicketHold, now: Date = new Date()): string | null {
  if (!hold.since || !WAITS.has(hold.case)) return null;
  const at = sinceClock(hold.since, localeOf(t), now);
  const n = hold.attempt ?? 0;
  return hold.case === "prerequisite"
    ? t("hold.wait.prerequisite", { names: namesOf(t, hold.names), count: hold.names.length, at })
    : t(`hold.wait.${hold.case as "model_wait" | "queued" | "resuming" | "restarting" | "ci_running" | "refreshing"}`, { n, at });
}

/** The hold's one sentence, from the hold alone — the same words on every surface; a stop quotes what it recorded.
 *  2026-11-23-31dci: a wait says since when and that nothing is to be done, and one that waited too long names the minutes
 *  and the key it outlasted and that it was reported to the admin. */
export function holdSentence(t: T, hold: TicketHold, now: Date = new Date()): string {
  const n = hold.attempt ?? 0;
  const names = namesOf(t, hold.names);
  const wait = waitClause(t, hold, now);
  // 2026-11-23-849e: a CI that ran past the minutes is a hung pipeline — the sentence says how long and what to check.
  if (hold.case === "ci_running" && hold.escalation && hold.since)
    return t("hold.escalated.ci_running", { duration: durationOf(hold.since, now), minutes: hold.escalation.afterMinutes, key: hold.escalation.key });
  // 2026-11-23-0219: a required build that never started, past the minutes, is reported the same way.
  if (hold.case === "ci_not_started" && hold.escalation && hold.since)
    return t("hold.escalated.ci_not_started", { duration: durationOf(hold.since, now), minutes: hold.escalation.afterMinutes, key: hold.escalation.key });
  if (wait && hold.escalation)
    return t("hold.escalated", { wait, minutes: hold.escalation.afterMinutes, key: hold.escalation.key });
  if (wait) return t("hold.nobody", { wait });
  const said = told(KIND_OF_CASE[hold.case] ?? null, hold.detail ?? null, t);
  switch (hold.case) {
    case "image_untrusted":
    case "bootstrap_missing":
    case "preflight":
      return said ?? t(`hold.sentence.${hold.case}`, { n });
    case "readiness_unknown": {
      const why = hold.detail?.subject;
      return why ? t("hold.sentence.readiness_unknown_why", { n, why }) : t("hold.sentence.readiness_unknown", { n });
    }
    case "failed": return said ? t("hold.sentence.failed_told", { n, told: said }) : t("hold.sentence.failed", { n });
    // 2026-11-24-6811d: a refresh that left the branch unchanged quotes its reason.
    case "refresh_refused":
      return hold.detail?.subject ? t("hold.sentence.refresh_refused_why", { why: hold.detail.subject }) : t("hold.sentence.refresh_refused");
    // 2026-11-23-d60d: red CI names its failed checks; a red read that recorded none says only that it failed.
    case "ci_failed":
      return hold.names.length > 0 ? t("hold.sentence.ci_failed", { names, count: hold.names.length }) : t("hold.sentence.ci_failed_bare");
    // 2026-11-23-849e: the series' one merge says what it unblocks and, where it does, that merging approves.
    case "merge_next": return [t("hold.sentence.merge_next"), mergeTail(t, hold), hold.approves ? t("hold.merge.approves") : null]
      .filter(Boolean).join(" ");
    case "starts_on_merge": {
      const count = hold.merges ?? hold.names.length;
      return t("hold.sentence.starts_on_merge", { count, next: hold.next ?? names });
    }
    case "merge_after":
    case "prerequisite":
      return t(`hold.sentence.${hold.case}`, { names, count: hold.names.length });
    default: return t(`hold.sentence.${hold.case}`, { n });
  }
}

/** The words a hold's act is drawn with on a button or a link. */
export function holdActLabel(t: T, hold: TicketHold): string | null {
  switch (hold.act?.kind) {
    case undefined: return null;
    case "answer": return t("hold.act.answer");
    case "conversation": return hold.act.target ? t("hold.act.plan") : t("hold.act.conversation");
    case "retry": return t("hold.act.retry");
    case "merge": return t("hold.act.merge");
    case "series": return t("hold.act.series");
    case "settings": return hold.case === "image_untrusted" && !hold.detail?.settingsPath ? t("hold.act.trust") : t("hold.act.settings");
    case "init": return t("hold.act.init");
    case "pull_request": return hold.act.target ? t("hold.act.pull_request") : null;
  }
}

/** 2026-11-23-849e: what merging the series' next merge unblocks — the step after it and the tickets that start only on
 *  merges; null when it names neither. 2026-11-23-f562: in the needed-merge wording of the series page. */
export function mergeTail(t: T, hold: TicketHold): string | null {
  const names = namesOf(t, hold.names);
  const count = hold.names.length;
  if (hold.next && count > 0) return t("hold.merge.tail.both", { next: hold.next, names, count });
  if (hold.next) return t("hold.merge.tail.next", { next: hold.next });
  return count > 0 ? t("hold.merge.tail.names", { names, count }) : null;
}

/** 2026-11-23-849e: how long since an instant — "45 min", "5 h", "3 d"; the units read the same in both languages. */
export function durationOf(iso: string, now: Date = new Date()): string {
  const minutes = Math.max(0, Math.floor((now.getTime() - new Date(iso).getTime()) / 60000));
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  return hours < 48 ? `${hours} h` : `${Math.floor(hours / 24)} d`;
}

/** The act a Your move row or a series row names — it opens the ticket, where the act is; talk is "discuss" there. */
export function holdRowAct(t: T, hold: TicketHold | null | undefined): string {
  if (!hold?.act) return t("hold.act.open");
  if (hold.act.kind === "conversation") return hold.act.target ? t("hold.act.plan") : t("hold.act.discuss");
  return holdActLabel(t, hold) ?? t("hold.act.open");
}

/** A person's move: merge, plan or decide. */
export function isPersons(hold: TicketHold | null | undefined): boolean {
  return hold?.addressee === "merge" || hold?.addressee === "plan" || hold?.addressee === "decide";
}

/** Whether a ticket is in Your move — a person's hold, and an admin's for a viewer who is an admin. */
export function isYourMove(ticket: TicketSummary, admin: boolean): boolean {
  return isPersons(ticket.hold) || (admin && ticket.hold?.addressee === "admin");
}

/** Your move's count as the server counted it — with the admin holds for an admin. */
export function yourMoveCount(counts: TicketCounts | null | undefined, admin: boolean): number | undefined {
  if (!counts) return undefined;
  return counts.needsYou + (admin ? counts.adminHolds ?? 0 : 0);
}

/** The list's counts as this viewer reads them: for an admin the admin holds are Your move's and no longer Stuck's. */
export function countsFor(counts: TicketCounts, admin: boolean): TicketCounts {
  const holds = admin ? counts.adminHolds ?? 0 : 0;
  return holds === 0 ? counts : { ...counts, needsYou: counts.needsYou + holds, stuck: Math.max(0, counts.stuck - holds) };
}

/** The line a Your move row says: the question or the hand-back itself when the hold is one, else the hold's sentence. */
export function holdAsk(t: T, ticket: TicketSummary): string {
  const hold = ticket.hold;
  if (isAsked(hold) && ticket.pendingQuestion) return ticket.pendingQuestion.question.text.split(/\r?\n/)[0];
  if (hold?.case === "handback" && ticket.handback?.text) return ticket.handback.text.split(/\r?\n/)[0];
  return hold ? holdSentence(t, hold) : "";
}

/** The series section's tag for a hold — whose it is. */
export function holdTag(t: T, hold: TicketHold): string {
  return t(`hold.tag.${hold.addressee}`);
}
