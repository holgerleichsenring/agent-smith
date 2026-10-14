"use client";

import type { ReactNode } from "react";
import type { TicketConversationEntry } from "@/types/tickets";
import { avatarOf, decisionPill, isQuote, nameOf, whereOf } from "./conversationWords";
import { clock } from "./flowWords";

// 2026-10-13-9a59r: one entry of the ticket's conversation, the mock's .msg — avatar by role, the name
// (author only where recorded), where-label, time, body (a person's words as a quote), a review comment's
// file and line, the detail block (a ticket-text diff, a hand-back's quote, a cut review's findings) and
// the footer. Keyed and anchored on 9a59h's composite id. Spec-cut entries are drawn internal.

interface Props {
  entry: TicketConversationEntry;
  attemptNumber: (runId: string | null) => number | null;
  /** The attempt this entry started — set only where causeEntryMatch joined it. */
  started: number | null;
}

export function ConversationEntry({ entry, attemptNumber, started }: Props) {
  const avatar = avatarOf(entry);
  const internal = entry.source === "spec-cut";
  const pill = decisionPill(entry);
  const startedNothing = entry.kind === "command-answer"
    && (entry.detail as { startedNothing?: unknown } | null | undefined)?.startedNothing === true;
  const footer = pill || started !== null || startedNothing;
  return (
    <div className={internal ? "msg internal" : "msg"} data-entry-id={entry.id} data-testid="conv-entry" data-source={entry.source}>
      <span className={`av ${avatar.className}`} aria-hidden="true">{avatar.text}</span>
      <div>
        <div className="mh">
          <b>{nameOf(entry)}</b>
          <span className="where">{whereOf(entry, attemptNumber)}</span>
          <span className="w">{clock(entry.at)}</span>
        </div>
        <Body entry={entry} />
        <DetailBlock entry={entry} />
        {footer && (
          <div className="mf">
            {pill && <span className={`pill ${pill[0]}`}>{pill[1]}</span>}
            {startedNothing && <span>command answered, no attempt</span>}
            {started !== null && <span className="eff run" data-testid="conv-started">started attempt {started}</span>}
          </div>
        )}
      </div>
    </div>
  );
}

function Body({ entry }: { entry: TicketConversationEntry }) {
  if (entry.kind === "edited") {
    return <div className="mb">The ticket text differs from what the attempt before read.</div>;
  }
  const where = entry.file ? `${entry.file}${entry.line != null ? ` · line ${entry.line}` : ""}` : null;
  if (!entry.text && !where) return null;
  return (
    <div className={isQuote(entry) ? "mb quote" : "mb"}>
      {where && <span className="file" data-testid="conv-file">{where}</span>}
      {entry.text}
    </div>
  );
}

interface EditedField { before?: string | null; after?: string | null }
interface Finding { specId?: string; criterion?: string; problem?: string; cites?: unknown }

function DetailBlock({ entry }: { entry: TicketConversationEntry }) {
  const detail = (entry.detail ?? null) as Record<string, unknown> | null;
  if (!detail) return null;
  const rows: ReactNode[] = [];
  if (entry.kind === "edited") {
    for (const field of ["title", "description", "criteria"]) {
      const change = detail[field] as EditedField | undefined;
      if (!change) continue;
      rows.push(<div key={`${field}-h`} className="dim-line">{field}</div>);
      if (change.before) rows.push(<div key={`${field}-rm`} className="rm">− {change.before}</div>);
      if (change.after) rows.push(<div key={`${field}-ad`} className="ad">+ {change.after}</div>);
    }
  } else if (entry.kind === "hand-back" && typeof detail.quote === "string" && detail.quote) {
    rows.push(<div key="quote">{detail.quote}</div>);
  } else if (entry.kind === "cut-review" && Array.isArray(detail.findings)) {
    (detail.findings as Finding[]).forEach((f, i) => {
      const head = [f.specId ? `spec ${f.specId}` : null, f.criterion].filter(Boolean).join(" · ");
      rows.push(<div key={`f-${i}`}>{head ? `${head} — ` : ""}{f.problem}</div>);
      for (const cite of Array.isArray(f.cites) ? f.cites : []) {
        if (typeof cite === "string") rows.push(<div key={`f-${i}-${cite}`} className="dim-line">{cite}</div>);
      }
    });
  }
  if (rows.length === 0) return null;
  return <div className="diff" data-testid="conv-detail">{rows}</div>;
}
