"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { HUB_URL } from "@/hooks/useJobsHub";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import {
  deleteSpecDialogConversation,
  fetchSpecDialog,
  fetchSpecDialogConversations,
  postSpecDialogMessage,
  uploadSpecDialogImage,
} from "@/lib/specDialogApi";
import { currentDialogId, returnToDialog, startNewDialog } from "@/lib/specDialogSession";
// 2026-09-17-042ee kept the turn's steps here; 2026-09-18-2f8b moved the merge out, because
// "which steps belong to the turn running now" is a rule of its own with its own tests.
import { mergedSteps, ofTurn } from "@/components/dialog/turnSteps";
// 2026-09-18-7a05: what a deletion does not undo, worded by what the conversation filed.
import { deletionWarning } from "@/components/dialog/conversationDelete";
// 2026-09-20-3af8: nothing ties an image to a turn, so where it sits is a rule of its own.
import { withImages } from "@/components/dialog/transcriptImages";
import type {
  SpecDialogDecision,
  SpecDialogFilingPush,
  SpecDialogImage,
  SpecDialogProposalPush,
  SpecDialogActivityPush,
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

// 2026-09-17-042el: an approve or reject is a DECISION, not something the operator said. The
// buttons add it locally because a read re-seeds only when the conversation changes, so the
// server's record of it would never appear live; after a reload the seed maps that record to the
// same entry. A typed "approve" is echoed as typed and reads back as the decision it was.
// The post is accepted before the message is routed, so a click is PENDING until a read issued after
// it says what was stored: the entry then becomes that decision, or goes, and the question card comes
// back from the same read when the question is still open.

export type DialogEntryKind = "user" | "agent" | "decision" | "image";

export interface DialogEntry {
  key: string;
  kind: DialogEntryKind;
  text: string;
  at: string;
  /** 2026-09-17-c7aed: the proposal this agent turn produced, shown as a card on it. Live every
   *  proposing turn keeps its own; after a reload only the latest is known. */
  proposal?: SpecDialogProposalPush;
  /** Set on a decision entry. */
  decision?: SpecDialogDecision;
  /** Set on a decision the page showed before any read confirmed the server stored it. */
  pending?: PendingDecision;
  /** 2026-09-20-3af8: set on an image entry — what the operator attached, by its address. */
  image?: SpecDialogImage;
}

export interface PendingDecision {
  /** The read count when it was shown; only a read issued after that can confirm it. */
  read: number;
  /** The transcript length the page last read; the stored decision is at or after it. */
  fromTurn: number;
}

/** The two decisions this page knows. The transcript shows any other stored value as the message it was. */
export function isDecision(value: unknown): value is SpecDialogDecision {
  return value === "approved" || value === "rejected";
}

export interface SpecDialogState {
  dialogId: string | null;
  view: SpecDialogView | null;
  /** The caller's conversations, open and closed — read on mount, when the session here changes,
   *  and after a framework message that the listed row for this conversation is behind. */
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
  /** 2026-09-18-2f8b: whether the working line belongs on the page at all — this page's own
   *  post OR a turn the view says is computing. The local flag is the FLOOR: no read is
   *  issued after a post, so a page rendering only from the view would go dark for the whole
   *  turn it just started. */
  working: boolean;
  /** The moment, on THIS browser's clock, the working line counts up from — derived once from
   *  the seconds the server says the turn has computed. Null when no turn is known to run. */
  workingSince: number | null;
  /** The repositories the running turn opened, one per repository at its latest state, in
   *  the order they were first opened. Empty again when a new turn starts or the answer arrives. */
  readings: SpecDialogReadingPush[];
  /** What the running turn did, oldest first. Cleared with the readings. */
  activity: SpecDialogActivityPush[];
  /** A decision is posted as its word and shown as a decision entry rather than echoed. */
  send: (text: string, project?: string, decision?: SpecDialogDecision) => Promise<void>;
  startNew: (project?: string) => Promise<void>;
  /** Continues a past conversation in this tab, on a dialog id of its own. */
  open: (sessionId: string, openDialogId?: string | null) => Promise<void>;
  /** 2026-09-18-7a05: deletes a conversation the caller owns, after saying what that does not
   *  undo. Deleting the one open here clears the surface and mints a fresh dialog id. */
  remove: (sessionId: string) => Promise<void>;
  /** 2026-09-20-3af8: an image beside what the operator is saying. It is stored against the
   *  conversation at once — opening one if none is open — and rides the NEXT turn. */
  attach: (file: File, project?: string) => Promise<void>;
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
  // 2026-09-18-2f8b: what the VIEW says about the turn — what a page that did not post the
  // message has instead of the flag above, and what a page that did post adds to it.
  const [computing, setComputing] = useState(false);
  const [workingSince, setWorkingSince] = useState<number | null>(null);
  // Whether the page is showing the working line at all, for the subscription below to ask
  // without being torn down and rebuilt every time the answer changes.
  const working = useRef(false);
  // 2026-09-17-c7aec: while awaiting, the server says which repositories the turn opened, so
  // the minute names what is being read.
  const [readings, setReadings] = useState<SpecDialogReadingPush[]>([]);
  // 2026-09-17-042ee: and what it does with them. Kept in arrival order and bounded: a long
  // turn calls a tool many times, and a page that grows without limit is its own defect.
  const [activity, setActivity] = useState<SpecDialogActivityPush[]>([]);
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
  // The reply a proposal follows added no entry when it was only the draft, so its card needs
  // a turn of its own rather than the agent line said before it.
  const replyWasDraftOnly = useRef(false);
  // The proposal the pane holds and the read count when it was learned. Only a read issued
  // AFTER that says anything about it: an earlier one may have left before the push arrived,
  // and one issued later that holds no proposal means the server let it go — a rejection or a
  // timed-out approval, which a reload would also show as gone.
  const known = useRef<KnownProposal | null>(null);
  // The same for a question: one pushed while a read was out is newer than what that read says.
  const askedAtRead = useRef<number | null>(null);
  // How long the transcript was at the latest read — where a decision clicked now will be stored.
  const turnsRead = useRef(0);

  // Reading the held id is a browser act, so it happens after the first render rather
  // than during it.
  useEffect(() => setDialogId(currentDialogId()), []);

  const append = useCallback((kind: DialogEntryKind, text: string, at: string, extra: Partial<DialogEntry> = {}) => {
    counter.current += 1;
    const key = `${kind}-${counter.current}`;
    setEntries((held) => [...held, { key, kind, text, at, ...extra }]);
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
      // 2026-09-18-2f8b: the turn as the server has it. The elapsed seconds are turned into a
      // moment on THIS clock once, so the counter below differences nothing across machines.
      // Read defensively: a payload from a server that does not carry the turn yet must leave
      // the page working, not raise a page-wide failure over a field.
      const turn = next.turn ?? null;
      setComputing(turn?.computing ?? false);
      if (turn?.computing) {
        setWorkingSince(Date.now() - turn.elapsedSeconds * 1000);
        setActivity((held) => mergedSteps(ofTurn(held, turn.turnStartedAt), turn.steps));
      }
      // The question lives only in this state and in the server's in-memory wait, so a
      // reload has to take it back from the read or the approval gate loses its card.
      if (askedAtRead.current === null || askedAtRead.current < issued) setQuestion(next.question);
      turnsRead.current = next.session?.transcript.length ?? 0;
      setEntries((held) => settled(held, next, issued));
      const armed = reseed.current;
      const live = known.current;
      const learnedBefore = live !== null && live.read < issued;
      if (armed === false || (typeof armed === "string" && next.session?.sessionId !== armed)) {
        if (learnedBefore && !next.session?.proposal) {
          known.current = null;
          setProposal(null);
          setEntries((held) => withoutCard(held, live.proposal));
        }
        return;
      }
      reseed.current = false;
      // A proposal pushed while this read was out is newer than anything the read carries.
      // The read may still have caught it stored, under a moment of its own: its card then
      // gives way to the live one rather than standing beside it.
      if (live !== null && !learnedBefore) {
        const seeded = seed(next);
        const stored = next.session?.proposal ?? null;
        const rest = stored && sameDraft(stored, live.proposal) ? withoutCard(seeded, stored) : seeded;
        // The pane already holds it, and whatever was filed after it; only the entries were replaced.
        setEntries(withCard(rest, live.proposal, live.ownTurn));
        return;
      }
      setEntries(seed(next));
      // The pane lived only in pushes, so a reload lost what was being decided and what was
      // filed. The session keeps both; the column takes them back the way the pushes set it.
      const held = heldOutcome(next);
      known.current = held.proposal ? { proposal: held.proposal, read: issued, ownTurn: false } : null;
      setProposal(held.proposal);
      setFiled(held.filed);
    } catch (thrown) {
      if (issued === reads.current) setFailure(asError(thrown));
    }
  }, []);

  // A command a CONTROL sent is not echoed: the operator clicked "new conversation", they
  // did not say "/spec". What they typed themselves is echoed, because the channel
  // delivers replies and never a copy of the message just sent.
  const post = useCallback(async (id: string, text: string, echo: boolean | SpecDialogDecision) => {
    // Cleared before the post: the turn starts on the server before the post returns, and its
    // first repository may be announced before this line would otherwise run.
    setReadings([]);
    setActivity([]);
    setWorkingSince(Date.now());
    try {
      await postSpecDialogMessage(id, text);
      if (echo === true) append("user", text, new Date().toISOString());
      else if (echo)
        append("decision", text, new Date().toISOString(), {
          decision: echo,
          pending: { read: reads.current, fromTurn: turnsRead.current },
        });
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

  // The list reads overlap exactly as the dialog reads do — one goes out per framework message —
  // and a late answer carrying the row as it was two replies ago would roll the title and the
  // turn count back to a moment that has passed. Its own sequence number says which is latest.
  const listReads = useRef(0);

  const loadConversations = useCallback(async () => {
    const issued = (listReads.current += 1);
    try {
      const next = await fetchSpecDialogConversations();
      if (issued !== listReads.current) return;
      setConversations(next);
    } catch (thrown) {
      // The list is beside the conversation, not the conversation: a failed read of it must not
      // put the page-wide failure over a dialog that is working.
      console.warn("the conversation list could not be read", thrown);
    }
  }, []);

  // 2026-09-17-042em: ON A REPLY THE LISTED ROW IS BEHIND, because the reply is sent after the
  // turn is appended. A conversation's title and turn count are read off its transcript, so the
  // read this effect makes when the session opens races the first append and leaves the running
  // conversation listed as untitled with zero turns until something else triggers a read. The
  // reply is the thing that happens: it is sent after the assistant turn is appended
  // (SpecDialogRouter.cs:105-106), so a read issued from it sees the turn. This narrows
  // 2026-09-17-c7aed's "never after every message" rather than reversing it: that rule priced the
  // read — every listed transcript parsed, two further JSON documents per row, up to fifty rows —
  // and the price is still real, so the read is issued only while it would say something new.
  // The filing's own read goes with it: the filing notice is a framework message like any other.
  const sessionHere = view?.session?.sessionId ?? null;
  useEffect(() => {
    void loadConversations();
  }, [sessionHere, loadConversations]);

  // What the list last said about the conversation open HERE. Held in a ref because the hub
  // subscription asks it: putting the list in that effect's dependencies would tear the
  // subscription down and rebuild it every time the list changed.
  const listedHere = useRef<SpecDialogSessionSummary | null>(null);
  useEffect(() => {
    listedHere.current =
      conversations.find((held) => held.sessionId === sessionHere) ?? null;
  }, [conversations, sessionHere]);

  /** Whether a list read would tell this page anything it does not already know: the conversation
   *  open here is not listed at all, is listed with no title, or is listed with fewer turns than
   *  the page last read the transcript to be. Once the row identifies the conversation it stops
   *  being read on every reply, and it comes back the moment the count falls behind again. */
  const listIsBehind = useCallback(() => {
    const row = listedHere.current;
    return row === null || row.title === null || row.turns < turnsRead.current;
  }, []);

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
      setComputing(false);
      setWorkingSince(null);
      setReadings([]);
      setActivity([]);
      // A reply that was nothing but a draft arrives empty: the proposal pane carries it.
      replyWasDraftOnly.current = message.text.trim().length === 0;
      if (!replyWasDraftOnly.current) append("agent", message.text, message.at);
      // Asked BEFORE the dialog read below, which is what moves turnsRead on: the question is
      // whether the list is behind what the page already knew, not behind what it is about to learn.
      const behind = listIsBehind();
      void load(dialogId);
      if (behind) void loadConversations();
    });
    const offQuestion = client.specDialogQuestions.add((asked) => {
      if (asked.dialogId !== dialogId) return;
      askedAtRead.current = reads.current;
      setQuestion(asked);
    });
    // A new proposal supersedes the last one AND whatever it was filed as: the column
    // moves back to what is being decided now.
    const offProposal = client.specDialogProposals.add((proposed) => {
      if (proposed.dialogId !== dialogId) return;
      setProposal(proposed);
      setFiled(null);
      const ownTurn = replyWasDraftOnly.current;
      known.current = { proposal: proposed, read: reads.current, ownTurn };
      setEntries((held) => withCard(held, proposed, ownTurn));
    });
    const offReading = client.specDialogReadings.add((reading) => {
      if (reading.dialogId === dialogId) setReadings((held) => upsertReading(held, reading));
    });
    const offActivity = client.specDialogActivity.add((step) => {
      if (step.dialogId !== dialogId) return;
      setActivity((held) => mergedSteps(held, [step]));
      // 2026-09-18-2f8b: a SECOND SCREEN that was already open when the turn started. The
      // computing flag comes off a read, and the only unconditional read is the one this page
      // made when it mounted — so a tab opened before the post folds in every step and renders
      // nothing at all. A step arriving while the page shows no working line is evidence a
      // turn is running, and the read is what turns that into the line and its clock.
      if (!working.current) void load(dialogId);
    });
    const offFiled = client.specDialogFilings.add((filing) => {
      if (filing.dialogId === dialogId) setFiled(filing);
    });
    // Everything pushed while the connection was down is gone — the reply that ends the turn
    // included. Without a read on the way back the page would keep counting up for a turn that
    // finished during the gap, and the next turn's steps would meet a list that outlived it.
    const offConnection = client.connectionState.add((connection) => {
      if (connection === HubConnectionState.Connected) void load(dialogId);
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
      offActivity();
      offConnection();
      void stop?.();
    };
  }, [dialogId, append, load, post, loadConversations, listIsBehind]);

  /// Sending with no session open used to reach the router as an ordinary message, and
  /// the router answered with the command tutorial a chat channel needs — on a page whose
  /// whole point is that nobody types a command. So the session is opened first, on the
  /// project the page already knows, and the message follows it.
  const send = useCallback(
    async (text: string, project?: string, decision?: SpecDialogDecision) => {
      const said = text.trim();
      if (!dialogId || said.length === 0) return;
      if (!view?.session) {
        if (!project) return;
        reseed.current = true;
        await post(dialogId, `/spec ${project}`, false);
      }
      await post(dialogId, said, decision ?? true);
    },
    [dialogId, view, post],
  );

  // 2026-09-20-3af8: the image is stored BEFORE any message follows it, and the server opens
  // the conversation when none is open — so an image pasted as the very first act is kept
  // rather than lost to the opening post it is racing. The read that follows is what puts it
  // in the transcript, and it is also what tells this page the conversation now exists.
  const attach = useCallback(
    async (file: File, project?: string) => {
      if (!dialogId) return;
      try {
        await uploadSpecDialogImage(dialogId, project ?? "", file);
        // The read has to RESEED: the image is not something this page said, so there is
        // nothing to echo locally, and an unarmed read leaves the entries as they were.
        reseed.current = true;
        await load(dialogId);
      } catch (thrown) {
        setFailure(asError(thrown));
      }
    },
    [dialogId, load],
  );

  // A fresh dialog id with a command queued for it: the command waits for the subscription,
  // so its answer lands in a group this page has joined.
  const switchTo = useCallback((command: string | null, awaited: boolean | string, to?: string) => {
    reseed.current = awaited;
    replyWasDraftOnly.current = false;
    known.current = null;
    askedAtRead.current = null;
    setEntries([]);
    setQuestion(null);
    setProposal(null);
    setFiled(null);
    setView(null);
    setAwaiting(false);
    setComputing(false);
    setWorkingSince(null);
    setReadings([]);
    setActivity([]);
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

  // 2026-09-18-7a05: a conversation the operator started by mistake, gone. It lives HERE rather
  // than in the list, because deleting the conversation open in the surface has to clear every
  // pane and mint a fresh dialog id — the held one now names a dead thread — and the switch that
  // does that in one act is local to this hook. What the list exports is the NEW-conversation
  // callback, which opens one on the fresh id: the opposite of a delete.
  const remove = useCallback(
    async (sessionId: string) => {
      const listed = conversations.find((held) => held.sessionId === sessionId);
      const warning = listed ? deletionWarning(listed) : null;
      if (warning !== null && !window.confirm(warning)) return;
      try {
        await deleteSpecDialogConversation(sessionId);
      } catch (thrown) {
        setFailure(asError(thrown));
        return;
      }
      // A list read ISSUED BEFORE THE DELETE AND LANDING AFTER IT still carries the row. The
      // sequence guard drops a read only when a NEWER read has been issued, so the counter is
      // bumped here: every read now outstanding is superseded and none of them can put the row
      // back. For the open conversation the switch below issues one of its own; for a row that
      // is not open nothing else would.
      listReads.current += 1;
      setConversations((held) => held.filter((row) => row.sessionId !== sessionId));
      if (sessionId === view?.session?.sessionId) switchTo(null, true);
    },
    [conversations, view, switchTo],
  );

  working.current = awaiting || computing;
  return {
    dialogId, view, conversations, entries, question, proposal, filed, failure, awaiting,
    working: working.current, workingSince,
    readings, activity, send, startNew, open, remove, attach,
  };
}

interface KnownProposal {
  proposal: SpecDialogProposalPush;
  /** The read count when the page learned it; reads issued up to it cannot have seen it. */
  read: number;
  /** Whether its card was a turn of its own, for a re-seed that has to put it back. */
  ownTurn: boolean;
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

/** A turn that was only a draft is kept when the card belongs on it, and dropped otherwise.
 *  2026-09-20-3af8: the conversation's images take their place among the turns by their moment. */
function seed(view: SpecDialogView): DialogEntry[] {
  const session = view.session;
  return withImages(turns(view), session?.images ?? []);
}

function turns(view: SpecDialogView): DialogEntry[] {
  const session = view.session;
  return (session?.transcript ?? [])
    .map((turn, index): DialogEntry => ({
      key: `held-${index}`,
      kind: turn.decision ? "decision" : turn.role === "user" ? "user" : "agent",
      text: turn.text,
      at: turn.at,
      ...(turn.decision ? { decision: turn.decision } : {}),
      ...(index === session?.proposalTurn && session.proposal ? { proposal: session.proposal } : {}),
    }))
    .filter((entry) => entry.kind !== "agent" || entry.text.trim().length > 0 || entry.proposal);
}

/** A pending decision meets the first read issued after it: the decision stored at or after the turn
 *  it was clicked at replaces it — each stored decision confirms one click — and without one it goes. */
function settled(held: DialogEntry[], view: SpecDialogView, issued: number): DialogEntry[] {
  if (!held.some((entry) => entry.pending && entry.pending.read < issued)) return held;
  const turns = view.session?.transcript ?? [];
  const claimed = new Set<number>();
  return held.flatMap((entry): DialogEntry[] => {
    const pending = entry.pending;
    if (!pending || pending.read >= issued) return [entry];
    const index = turns.findIndex(
      (turn, at) => at >= pending.fromTurn && !claimed.has(at) && isDecision(turn.decision));
    if (index < 0) return [];
    claimed.add(index);
    const turn = turns[index];
    return isDecision(turn.decision)
      ? [{ key: entry.key, kind: "decision", text: turn.text, at: turn.at, decision: turn.decision }]
      : [];
  });
}

/** A proposal follows the reply it came from, so its card goes on that reply — or on a turn of
 *  its own when the reply was nothing but the draft and so added no entry. */
function withCard(
  held: DialogEntry[],
  proposal: SpecDialogProposalPush,
  ownTurn: boolean,
): DialogEntry[] {
  const last = held.at(-1);
  if (!ownTurn && last?.kind === "agent" && !last.proposal) {
    return [...held.slice(0, -1), { ...last, proposal }];
  }
  return [...held, { key: `card-${proposal.at}-${held.length}`, kind: "agent", text: "", at: proposal.at, proposal }];
}

/** The proposal the server let go takes its card with it; a turn that was only that card goes too. */
function withoutCard(held: DialogEntry[], proposal: SpecDialogProposalPush): DialogEntry[] {
  return held
    .map((entry) => {
      if (entry.proposal !== proposal) return entry;
      const rest = { ...entry };
      delete rest.proposal;
      return rest;
    })
    .filter((entry) => entry.kind !== "agent" || entry.text.trim().length > 0 || entry.proposal);
}

/** The same draft, whichever moment it was stamped with — a push and a read stamp it differently. */
function sameDraft(a: SpecDialogProposalPush, b: SpecDialogProposalPush): boolean {
  const draft = ({ kind, bug, phase, parent, children }: SpecDialogProposalPush) =>
    JSON.stringify({ kind, bug, phase, parent, children });
  return draft(a) === draft(b);
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
