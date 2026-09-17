"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { HUB_URL } from "@/hooks/useJobsHub";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { fetchSpecDialog, postSpecDialogMessage } from "@/lib/specDialogApi";
import { currentDialogId, startNewDialog } from "@/lib/specDialogSession";
import type {
  SpecDialogFilingPush,
  SpecDialogProposalPush,
  SpecDialogQuestionPush,
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
  entries: DialogEntry[];
  question: SpecDialogQuestionPush | null;
  /** What this turn would file, until a later turn supersedes it. */
  proposal: SpecDialogProposalPush | null;
  /** What filing it actually created — the column's last state. */
  filed: SpecDialogFilingPush | null;
  failure: Error | null;
  /** True between a post and the answer it will get — the page's only progress signal. */
  awaiting: boolean;
  send: (text: string, project?: string) => Promise<void>;
  startNew: (project?: string) => Promise<void>;
  resume: (sessionId: string) => Promise<void>;
}

export function useSpecDialog(): SpecDialogState {
  const [dialogId, setDialogId] = useState<string | null>(null);
  const [view, setView] = useState<SpecDialogView | null>(null);
  const [entries, setEntries] = useState<DialogEntry[]>([]);
  const [question, setQuestion] = useState<SpecDialogQuestionPush | null>(null);
  const [proposal, setProposal] = useState<SpecDialogProposalPush | null>(null);
  const [filed, setFiled] = useState<SpecDialogFilingPush | null>(null);
  const [failure, setFailure] = useState<Error | null>(null);
  // A design turn materialises the scope's repositories and reads them: a minute of
  // nothing is normal. The channel pushes no progress — SendProgressAsync is a no-op
  // here, and on a chat platform that is right, because the platform shows the message
  // was delivered and people expect an answer later. A page that shows nothing at all
  // for a minute reads as broken, and the first question it produced was "is anything
  // happening?". The page does not need the server for this: it posted, and it has had
  // no reply yet.
  const [awaiting, setAwaiting] = useState(false);
  // The transcript is re-seeded from the server only when the CONVERSATION changed — a
  // refetch after every reply would otherwise drop the framework's own lines, which the
  // durable transcript does not hold.
  const reseed = useRef(true);
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
      if (!reseed.current) return;
      reseed.current = false;
      setEntries(seed(next));
    } catch (thrown) {
      if (issued === reads.current) setFailure(asError(thrown));
    }
  }, []);

  // A command a CONTROL sent is not echoed: the operator clicked "new conversation", they
  // did not say "/spec". What they typed themselves is echoed, because the channel
  // delivers replies and never a copy of the message just sent.
  const post = useCallback(async (id: string, text: string, echo: boolean) => {
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
      append("agent", message.text, message.at);
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
    const offFiled = client.specDialogFilings.add((filing) => {
      if (filing.dialogId === dialogId) setFiled(filing);
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
      void stop?.();
    };
  }, [dialogId, append, load, post]);

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

  const startNew = useCallback(async (project?: string) => {
    reseed.current = true;
    setEntries([]);
    setQuestion(null);
    setProposal(null);
    setFiled(null);
    setView(null);
    setAwaiting(false);
    // The router parses the same commands a chat channel types; the page is what spares
    // the operator from typing them.
    pending.current = project ? `/spec ${project}` : "/spec";
    setDialogId(startNewDialog());
  }, []);

  const resume = useCallback(
    async (sessionId: string) => {
      // The resumed session brings its own transcript, so the next read re-seeds.
      reseed.current = true;
      // The resumed conversation has its own outcome; the column falls back to the scope
      // rather than keeping the proposal of the one being left.
      setProposal(null);
      setFiled(null);
      if (dialogId) await post(dialogId, `/spec resume ${sessionId}`, false);
    },
    [dialogId, post],
  );

  return {
    dialogId, view, entries, question, proposal, filed, failure, awaiting,
    send, startNew, resume,
  };
}

function seed(view: SpecDialogView): DialogEntry[] {
  return (view.session?.transcript ?? []).map((turn, index) => ({
    key: `held-${index}`,
    kind: turn.role === "user" ? "user" : "agent",
    text: turn.text,
    at: turn.at,
  }));
}

function asError(thrown: unknown): Error {
  return thrown instanceof Error ? thrown : new Error(String(thrown));
}
