"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { HUB_URL } from "@/hooks/useJobsHub";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import {
  fetchSpecDialog,
  fetchSpecDialogConversations,
  postSpecDialogMessage,
} from "@/lib/specDialogApi";
import { currentDialogId, returnToDialog, startNewDialog } from "@/lib/specDialogSession";
import type {
  SpecDialogFilingPush,
  SpecDialogProposalPush,
  SpecDialogQuestionPush,
  SpecDialogReadingPush,
  SpecDialogSessionSummary,
  SpecDialogView,
} from "@/types/spec-dialog";

// 2026-09-15-cb3e: one design conversation, held by the page. The dialog id comes from the
// browser and survives a reload; the transcript is seeded from the durable one and then
// grows from what arrives — the operator's own message is echoed locally because the
// channel delivers replies, never a copy of what was just sent.
//
// A refetch after every reply is what keeps the SCOPE honest: a reply may have opened a
// session, resumed another, or re-scoped this one, and a stale scope panel is a claim
// about what the agent has read.

export type DialogEntryKind = "user" | "agent";

export interface DialogEntry {
  key: string;
  kind: DialogEntryKind;
  text: string;
  at: string;
}

export interface SpecDialogState {
  dialogId: string | null;
  view: SpecDialogView | null;
  /** The caller's conversations, open and closed — read on mount, when the session here
   *  changes, and after a filing; never after every message. */
  conversations: SpecDialogSessionSummary[];
  entries: DialogEntry[];
  question: SpecDialogQuestionPush | null;
  /** What this turn would file, until a later turn supersedes it. */
  proposal: SpecDialogProposalPush | null;
  /** What filing it actually created — the column's last state. */
  filed: SpecDialogFilingPush | null;
  failure: Error | null;
  /** True between a post and the answer it will get. */
  awaiting: boolean;
  /** The repositories the running turn opened, one per repository at its latest state, in
   *  the order they were first opened. Empty again when a new turn starts or the answer arrives. */
  readings: SpecDialogReadingPush[];
  send: (text: string, project?: string) => Promise<void>;
  startNew: (project?: string) => Promise<void>;
  /** Continues a past conversation in this tab, on a dialog id of its own. */
  open: (sessionId: string, openDialogId?: string | null) => Promise<void>;
}

export function useSpecDialog(): SpecDialogState {
  const [dialogId, setDialogId] = useState<string | null>(null);
  const [view, setView] = useState<SpecDialogView | null>(null);
  const [conversations, setConversations] = useState<SpecDialogSessionSummary[]>([]);
  const [entries, setEntries] = useState<DialogEntry[]>([]);
  const [question, setQuestion] = useState<SpecDialogQuestionPush | null>(null);
  const [proposal, setProposal] = useState<SpecDialogProposalPush | null>(null);
  const [filed, setFiled] = useState<SpecDialogFilingPush | null>(null);
  const [failure, setFailure] = useState<Error | null>(null);
  // A design turn materialises the scope's repositories and reads them: a minute of
  // nothing is normal. SendProgressAsync is a no-op on this channel, and on a chat platform
  // that is right, because the platform shows the message was delivered and people expect
  // an answer later. A page that shows nothing at all
  // for a minute reads as broken, and the first question it produced was "is anything
  // happening?". The page does not need the server for this: it posted, and it has had
  // no reply yet.
  const [awaiting, setAwaiting] = useState(false);
  // 2026-09-17-c7aec: while awaiting, the server says which repositories the turn opened, so
  // the minute names what is being read.
  const [readings, setReadings] = useState<SpecDialogReadingPush[]>([]);
  // The transcript is re-seeded from the server only when the CONVERSATION changed — a
  // refetch after every reply would otherwise drop the framework's own lines, which the
  // durable transcript does not hold. A session id means "re-seed once the read shows THAT
  // session": opening a past conversation reads its fresh dialog id before the resume has
  // moved anything there, and a plain flag would be spent on that empty read.
  const reseed = useRef<boolean | string>(true);
  // A command for a dialog id nobody is subscribed to yet would have its answer pushed
  // into a group this page has not joined, so it waits for the subscription.
  const pending = useRef<string | null>(null);
  const counter = useRef(0);

  // Reading the held id is a browser act, so it happens after the first render rather
  // than during it.
  useEffect(() => setDialogId(currentDialogId()), []);

  const append = useCallback((kind: DialogEntryKind, text: string, at: string) => {
    counter.current += 1;
    const key = `${kind}-${counter.current}`;
    setEntries((held) => [...held, { key, kind, text, at }]);
  }, []);

  // Reads overlap: every hub message triggers one, and a stale answer arriving late would
  // roll the scope column back to a moment that has passed. The sequence number is what
  // says which answer is still the latest.
  const reads = useRef(0);

  const load = useCallback(async (id: string) => {
    const issued = (reads.current += 1);
    try {
      const next = await fetchSpecDialog(id);
      if (issued !== reads.current) return;
      setView(next);
      // The question lives only in this state and in the server's in-memory wait, so a
      // reload has to take it back from the read or the approval gate loses its card.
      setQuestion(next.question);
      const armed = reseed.current;
      if (armed === false) return;
      if (typeof armed === "string" && next.session?.sessionId !== armed) return;
      reseed.current = false;
      setEntries(seed(next));
      // The pane lived only in pushes, so a reload lost what was being decided and what was
      // filed. The session keeps both; the column takes them back the way the pushes set it.
      const held = heldOutcome(next);
      setProposal(held.proposal);
      setFiled(held.filed);
    } catch (thrown) {
      if (issued === reads.current) setFailure(asError(thrown));
    }
  }, []);

  // A command a CONTROL sent is not echoed: the operator clicked "new conversation", they
  // did not say "/spec". What they typed themselves is echoed, because the channel
  // delivers replies and never a copy of the message just sent.
  const post = useCallback(async (id: string, text: string, echo: boolean) => {
    // Cleared before the post: the turn starts on the server before the post returns, and its
    // first repository may be announced before this line would otherwise run.
    setReadings([]);
    try {
      await postSpecDialogMessage(id, text);
      if (echo) append("user", text, new Date().toISOString());
      setQuestion(null);
      setAwaiting(true);
    } catch (thrown) {
      setAwaiting(false);
      setFailure(asError(thrown));
    }
  }, [append]);

  useEffect(() => {
    if (dialogId) void load(dialogId);
  }, [dialogId, load]);

  const loadConversations = useCallback(async () => {
    try {
      setConversations(await fetchSpecDialogConversations());
    } catch (thrown) {
      // The list is beside the conversation, not the conversation: a failed read of it must not
      // put the page-wide failure over a dialog that is working.
      console.warn("the conversation list could not be read", thrown);
    }
  }, []);

  // Not after every message: the list reads every listed transcript for its titles. A
  // session opened, resumed or forked here changes what it holds, and so does a filing.
  const sessionHere = view?.session?.sessionId ?? null;
  useEffect(() => {
    void loadConversations();
  }, [sessionHere, loadConversations]);

  useEffect(() => {
    if (!dialogId) return;
    const client = getJobsHubClient(HUB_URL);
    let cancelled = false;
    let stop: (() => Promise<void>) | null = null;
    const offMessage = client.specDialogMessages.add((message) => {
      if (message.dialogId !== dialogId) return;
      // Every message is answered — a reply, a refusal, or the turn-failed notice — so
      // this is where the waiting ends, whatever the answer turned out to be.
      setAwaiting(false);
      setReadings([]);
      // A reply that was nothing but a draft arrives empty: the proposal pane carries it.
      if (message.text.trim().length > 0) append("agent", message.text, message.at);
      void load(dialogId);
    });
    const offQuestion = client.specDialogQuestions.add((asked) => {
      if (asked.dialogId === dialogId) setQuestion(asked);
    });
    // A new proposal supersedes the last one AND whatever it was filed as: the column
    // moves back to what is being decided now.
    const offProposal = client.specDialogProposals.add((proposed) => {
      if (proposed.dialogId !== dialogId) return;
      setProposal(proposed);
      setFiled(null);
    });
    const offReading = client.specDialogReadings.add((reading) => {
      if (reading.dialogId === dialogId) setReadings((held) => upsertReading(held, reading));
    });
    const offFiled = client.specDialogFilings.add((filing) => {
      if (filing.dialogId !== dialogId) return;
      setFiled(filing);
      void loadConversations();
    });
    client.subscribeSpecDialog(dialogId)
      .then((cancel) => {
        if (cancelled) return void cancel();
        stop = cancel;
        const queued = pending.current;
        pending.current = null;
        if (queued) void post(dialogId, queued, false);
      })
      .catch((thrown) => setFailure(asError(thrown)));
    return () => {
      cancelled = true;
      offMessage();
      offQuestion();
      offProposal();
      offFiled();
      offReading();
      void stop?.();
    };
  }, [dialogId, append, load, post, loadConversations]);

  /// Sending with no session open used to reach the router as an ordinary message, and
  /// the router answered with the command tutorial a chat channel needs — on a page whose
  /// whole point is that nobody types a command. So the session is opened first, on the
  /// project the page already knows, and the message follows it.
  const send = useCallback(
    async (text: string, project?: string) => {
      const said = text.trim();
      if (!dialogId || said.length === 0) return;
      if (!view?.session) {
        if (!project) return;
        reseed.current = true;
        await post(dialogId, `/spec ${project}`, false);
      }
      await post(dialogId, said, true);
    },
    [dialogId, view, post],
  );

  // A fresh dialog id with a command queued for it: the command waits for the subscription,
  // so its answer lands in a group this page has joined.
  const switchTo = useCallback((command: string | null, awaited: boolean | string, to?: string) => {
    reseed.current = awaited;
    setEntries([]);
    setQuestion(null);
    setProposal(null);
    setFiled(null);
    setView(null);
    setAwaiting(false);
    setReadings([]);
    pending.current = command;
    setDialogId(to ? returnToDialog(to) : startNewDialog());
  }, []);

  // The router parses the same commands a chat channel types; the page is what spares the
  // operator from typing them.
  const startNew = useCallback(async (project?: string) => {
    switchTo(project ? `/spec ${project}` : "/spec", true);
  }, [switchTo]);

  // A dialog id is a tab, not a conversation. Resuming onto the id this tab holds would
  // close the conversation open on it; a fresh id has nothing open, so the resume closes
  // nothing. A conversation open in another tab moves here, and that tab finds out on its
  // next message.
  //
  // An OPEN conversation is already somewhere, so the page goes there and resumes nothing: a
  // resume is refused while a turn runs, and a person who left a conversation mid-turn would
  // otherwise be told to answer "there" with no way back to it — its reply and a waiting
  // approval going to a group nobody listens to until the approval times out.
  const open = useCallback(
    async (sessionId: string, openDialogId?: string | null) => {
      if (sessionId === view?.session?.sessionId) return;
      if (openDialogId) {
        switchTo(null, sessionId, openDialogId);
        return;
      }
      switchTo(`/spec resume ${sessionId}`, sessionId);
    },
    [view, switchTo],
  );

  return {
    dialogId, view, conversations, entries, question, proposal, filed, failure, awaiting,
    readings, send, startNew, open,
  };
}

/** A repository keeps its place; a later state replaces the earlier one. */
function upsertReading(
  held: SpecDialogReadingPush[],
  reading: SpecDialogReadingPush,
): SpecDialogReadingPush[] {
  const at = held.findIndex((line) => line.repo === reading.repo);
  if (at < 0) return [...held, reading];
  return held.map((line, index) => (index === at ? reading : line));
}

function seed(view: SpecDialogView): DialogEntry[] {
  return (view.session?.transcript ?? [])
    .map((turn, index) => ({
      key: `held-${index}`,
      kind: (turn.role === "user" ? "user" : "agent") as DialogEntryKind,
      text: turn.text,
      at: turn.at,
    }))
    .filter((entry) => entry.kind === "user" || entry.text.trim().length > 0);
}

/** A filing older than the proposal filed an earlier one, and the proposal outranks it —
 *  the same move a live proposal push makes when it clears the filing. */
function heldOutcome(view: SpecDialogView): {
  proposal: SpecDialogProposalPush | null;
  filed: SpecDialogFilingPush | null;
} {
  const proposal = view.session?.proposal ?? null;
  const filing = view.session?.filing ?? null;
  const superseded = proposal !== null && filing !== null && Date.parse(filing.at) < Date.parse(proposal.at);
  return { proposal, filed: superseded ? null : filing };
}

function asError(thrown: unknown): Error {
  return thrown instanceof Error ? thrown : new Error(String(thrown));
}
