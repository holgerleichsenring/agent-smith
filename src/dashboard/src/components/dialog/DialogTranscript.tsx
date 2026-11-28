"use client";

import { useDateLocale, useT } from "@/hooks/useT";
import type { ReactNode } from "react";
import { Markdown } from "@/components/ui/Markdown";
import { specDialogImageUrl } from "@/lib/specDialogApi";
import { isDecision, type DialogEntry } from "@/hooks/useSpecDialog";
import type { SpecDialogImage, SpecDialogReferenceSet } from "@/types/spec-dialog";
import { AuthedImage } from "./AuthedImage";
import { MockShot } from "./MockShot";
import { TurnMessage } from "./TurnMessage";
import { turnProposalOf } from "./turnProposal";
import { DialogDocumentCard } from "./DialogDocumentCard";
import { DialogDetailsBlock } from "./DialogDetailsBlock";
import { documentBlocks } from "./documentBlocks";
import { sizeOf } from "./referenceSelection";
import { sinceClock } from "@/components/tickets/holds/holdWords";

// 2026-09-15-cb3e: the conversation in order. What the agent sent is MARKDOWN and is
// rendered as such — the framework composes its lines for this channel and the design
// master writes ordinary markdown, so both read as formatting rather than as punctuation.
// What the operator typed is rendered as the plain text they typed.
// 2026-09-17-c7aed: a turn that proposed something carries a card, and a turn that was only
// the draft is the card alone.
// 2026-09-17-042ej: the closing line of the empty state says where the conversation goes after
// filing, because it no longer stops there: the filed tab follows the run.
// 2026-09-17-042el: an approve or reject is shown as the decision it was, not as the word — as
// pending until a read confirms the server stored it. A decision this page cannot name is shown as
// the message it was.
// 2026-09-17-042ef: the eyebrows are the studio's field label and the speaker's initials are a
// mark of this page's own — the studio's card icon leads a card, this leads a line.
// 2026-09-20-3af8: an image the operator attached is one of their own lines, shown where they
// attached it. The bytes come from a route of their own, so the transcript read stays small.
// 2026-10-01-aeb6b: a document in an agent turn is a card of its own, between the prose around it.
// 2026-10-01-283db: an uploaded website is one of the operator's lines too, as ONE chip — its name,
// how many files and how large — however many files it holds.
// 2026-10-09-86e1: and the chip OPENS that upload in the Uploads tab, where its files are.
// 2026-11-10-d854b: a turn that proposed something shows its reduced result (TurnResult) in place of the card, its
// reply as the summary; only the proposal awaiting a decision carries the approve bar.
// 2026-11-12-92e9c: every agent turn is one TurnMessage — steps, reply, spec lines, questions and its course — with no
// control inside it; the approval card stands below the conversation.
// 2026-11-17-d762b: the speakers are the words "you" and "agent", and an upload is one line of text.
// 2026-11-12-92e9d: the decision moved to the composer. A decision and the notice that answers it (Filed, Rejected)
// are quiet history lines: no speaker mark, muted, nothing new appended.
// 2026-11-26-f511c: where asked (the series chat), a turn carries its time under the speaker — "14:05" today, "Wed 14:05"
// within the week, a date beyond (sinceClock), the full date and time on hover — and a turn within a minute of the one
// before shows none. An agent text's trailing "↳ " lines (the acts its beat took) are one small muted line under it.

export function DialogTranscript({
  entries,
  greeting,
  onOpenUpload,
  from = 0,
  times = false,
}: {
  entries: DialogEntry[];
  /** 2026-11-26-5201a: the first entry to render — a chat window renders the newest ones; the entry before it still
   *  tells whether the first one answers a decision. */
  from?: number;
  /** 2026-11-26-f511c: show each turn's time (the series chat). */
  times?: boolean;
  /** 2026-10-09-86e1: show one upload's files — the pane's Uploads tab, opened on it. */
  onOpenUpload?: (setId: string) => void;
  /** 2026-09-27-481bd: what an empty conversation says. Computed by the surface, which is where
   *  the identity is read — a greeting decided here would put a fetch in every test that renders
   *  a transcript. */
  greeting?: ReactNode;
}) {
  if (entries.length === 0) {
    // 2026-09-27-481bd: 154 words of instruction used to stand here — how to phrase a request, how
    // long the first reply takes, and what the proposal card does, shown before any card exists,
    // to somebody who came to this page on purpose. The composer already says work can begin.
    return (
      <div data-testid="dialog-transcript-empty" className="dsh-h3 text-ink">
        {greeting}
      </div>
    );
  }

  return (
    <div data-testid="dialog-transcript" className="flex flex-col gap-4">
      {entries.slice(from).map((entry, i) =>
        answersDecision(entries, from + i) ? (
          <Notice key={entry.key} text={entry.text} />
        ) : (
          <Turn key={entry.key} entry={entry} onOpenUpload={onOpenUpload}
            at={times && stamped(entries, from + i) ? entry.at : undefined} />
        ),
      )}
    </div>
  );
}

function Turn({ entry, onOpenUpload, at }: { entry: DialogEntry; onOpenUpload?: (setId: string) => void; at?: string }) {
  if (entry.kind === "decision" && isDecision(entry.decision)) return <Decision entry={entry} />;
  if (entry.kind === "image" && entry.image) return <Attached image={entry.image} />;
  if (entry.kind === "reference" && entry.reference) return <ReferenceSetChip set={entry.reference} onOpen={onOpenUpload} />;
  if (entry.kind === "mock" && entry.mock) return <MockShot mock={entry.mock} />;
  const mine = entry.kind !== "agent";
  const said = entry.text.trim().length > 0;
  return (
    <DialogMessage who={mine ? "user" : "agent"} testId={said ? `dialog-turn-${mine ? "user" : "agent"}` : "dialog-turn-card"} at={at}>
      {mine ? (
        <p className="dsh-body whitespace-pre-wrap text-ink">{entry.text}</p>
      ) : (
        <TurnMessage proposal={entry.proposal ? turnProposalOf(entry.proposal) : null} runId={entry.runId} live={entry.steps}
          reply={entry.interrupted ? <Interrupted state={entry.interrupted} /> : said ? <AgentText text={entry.text} /> : undefined} />
      )}
    </DialogMessage>
  );
}

/** 2026-11-11-205ff: the summary of a turn no kept turn carries — still running elsewhere, or stopped before it
 *  finished; its draft is below. */
function Interrupted({ state }: { state: "running" | "stopped" }) {
  const t = useT();
  return (
    <p className="dsh-body text-mute" data-testid={state === "running" ? "dialog-turn-running" : "dialog-turn-interrupted"}>
      {state === "running"
        ? t("dialog.transcript.running")
        : t("dialog.transcript.stopped")}
    </p>
  );
}

function AgentText({ text }: { text: string }) {
  const { body, acts } = actLines(text);
  return (
    <>
      {documentBlocks(body).map((part, i) =>
        part.kind === "document" ? (
          <DialogDocumentCard key={i} text={part.text} />
        ) : part.kind === "details" ? (
          <DialogDetailsBlock key={i} text={part.text} />
        ) : (
          <Markdown key={i}>{part.text}</Markdown>
        ),
      )}
      {acts.length > 0 && <p className="dl-acts" data-testid="dialog-turn-acts">{acts.join(" · ")}</p>}
    </>
  );
}

/** 2026-11-26-f511c: an agent text's trailing lines that start with "↳ " — the acts a series beat took, which the server puts
 *  under the master's sentence — split from the text before them; a text without them is all body. */
export function actLines(text: string): { body: string; acts: string[] } {
  const lines = text.trimEnd().split("\n");
  let at = lines.length;
  while (at > 0 && lines[at - 1].startsWith("↳ ")) at--;
  if (at === lines.length || at === 0) return { body: text, acts: [] };
  return { body: lines.slice(0, at).join("\n").trimEnd(), acts: lines.slice(at).map((line) => line.trim()) };
}

/** 2026-11-26-f511c: a turn shows its time unless the one before it came within a minute. */
function stamped(entries: DialogEntry[], at: number): boolean {
  const now = Date.parse(entries[at].at);
  if (Number.isNaN(now)) return false;
  const before = at > 0 ? Date.parse(entries[at - 1].at) : Number.NaN;
  return Number.isNaN(before) || now - before >= 60_000;
}

function Attached({ image }: { image: SpecDialogImage }) {
  const t = useT();
  return (
    <DialogMessage who="user" testId="dialog-turn-image">
      <AuthedImage
        testId={`dialog-image-${image.id}`}
        path={specDialogImageUrl(image.id)}
        alt={t("common.attachedByYou")}
        className="max-h-64 max-w-full"
      />
    </DialogMessage>
  );
}

function ReferenceSetChip({ set, onOpen }: { set: SpecDialogReferenceSet; onOpen?: (setId: string) => void }) {
  const t = useT();
  // 2026-11-17-d762b: one plain line — "upload name · n files · size · show files" — with show files a text link.
  return (
    <DialogMessage who="user" testId="dialog-turn-reference">
      <p data-testid={`dialog-reference-${set.setId}`} className="dl-upload">
        <span className="dl-dim">{t("dialog.transcript.upload")}</span> <span className="mono given">{set.name}</span>
        {` · ${t("common.files", { count: set.files })} · ${sizeOf(set.bytes)}`}
        {onOpen && (
          <>
            {" · "}
            <button type="button" className="d-link" data-testid={`dialog-reference-open-${set.setId}`}
              onClick={() => onOpen(set.setId)}>
              {t("dialog.transcript.showFiles")}
            </button>
          </>
        )}
      </p>
    </DialogMessage>
  );
}

/** The framework's answer to a recorded decision: the agent entry right after it, proposing nothing. */
function answersDecision(entries: DialogEntry[], at: number): boolean {
  const entry = entries[at];
  const before = at > 0 ? entries[at - 1] : null;
  return entry.kind === "agent" && !entry.proposal && !entry.interrupted && entry.text.trim().length > 0
    && before?.kind === "decision" && isDecision(before.decision) && !before.pending;
}

function Notice({ text }: { text: string }) {
  return (
    <div data-testid="dialog-turn-notice" className="dsh-body text-mute">
      <Markdown>{text}</Markdown>
    </div>
  );
}

function Decision({ entry }: { entry: DialogEntry }) {
  const t = useT();
  const approved = entry.decision === "approved";
  return (
    <div data-testid="dialog-turn-decision">
      <p data-decision={entry.decision} data-pending={entry.pending ? "true" : undefined} className="dsh-body text-mute">
        {entry.pending
          ? t(approved ? "dialog.transcript.fileSent" : "dialog.transcript.discardSent")
          : t(approved ? "dialog.transcript.approved" : "dialog.transcript.rejected")}
      </p>
    </div>
  );
}

/** One row of the exchange: who said it, and what. The working line uses it too. */
export function DialogMessage({
  who,
  testId,
  children,
  at,
}: {
  who: "user" | "agent";
  testId?: string;
  children: ReactNode;
  /** 2026-11-26-f511c: the turn's time, shown under the speaker; absent shows none. */
  at?: string;
}) {
  const t = useT();
  const locale = useDateLocale();
  return (
    // 2026-11-17-d762b: the speaker is a small muted word in a narrow column, not a badge.
    <div data-testid={testId} className="grid grid-cols-[44px_minmax(0,1fr)] gap-2.5">
      <div className="min-w-0">
        <div
          aria-hidden="true"
          className={who === "user" ? "d-who" : "d-who agent"}
        >
          {t(who === "user" ? "dialog.transcript.you" : "dialog.transcript.agent")}
        </div>
        {at && (
          <time className="dl-time" dateTime={at} data-testid="dialog-turn-time"
            title={new Date(at).toLocaleString(locale, { dateStyle: "full", timeStyle: "short" })}>
            {sinceClock(at, locale, new Date(), true)}
          </time>
        )}
      </div>
      <div className="min-w-0">
        <span className="sr-only">{who === "user" ? t("dialog.transcript.youSaid") : "agent-smith:"}</span>
        {children}
      </div>
    </div>
  );
}
