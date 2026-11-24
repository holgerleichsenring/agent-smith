import { describe, expect, it } from "vitest";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { translator } from "@/lib/i18n/translate";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { seriesNodeKind } from "../seriesNodeKind";
import { seriesNodeSentence, seriesTicketActs } from "../seriesTicketActs";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-f562: every ticket's acts by its colour, each pointing at a real act or route, and the sentence why.

const t = translator("en");
const acts = (ticketId: string, lacks = false) => {
  const detail = overviewDetail();
  const ticket = detail.tickets.find((candidate) => candidate.ticketId === ticketId)!;
  return seriesTicketActs(ticket, detail, seriesNodeKind(ticket, detail), lacks, t);
};
const with_ = (ticketId: string, over: Partial<SeriesRunTicket>): { detail: SeriesRunDetail; ticket: SeriesRunTicket } => {
  const detail = overviewDetail();
  const at = detail.tickets.findIndex((candidate) => candidate.ticketId === ticketId);
  detail.tickets[at] = { ...detail.tickets[at], ...over };
  return { detail, ticket: detail.tickets[at] };
};
const shapeOf = ({ detail, ticket }: { detail: SeriesRunDetail; ticket: SeriesRunTicket }) =>
  seriesTicketActs(ticket, detail, seriesNodeKind(ticket, detail), false, t).map((a) => `${a.id}:${a.kind}${a.href ? `>${a.href}` : ""}${a.run ? `@${a.run}` : ""}`);
const shape = (ticketId: string, lacks = false) => acts(ticketId, lacks).map((a) => `${a.id}:${a.kind}${a.href ? `>${a.href}` : ""}${a.disabledBy ? ":disabled" : ""}`);

describe("seriesTicketActs", () => {
  it("SeriesTicketActs_MergeNow_MergeOpenPrTicketChain", () => {
    expect(shape("50")).toEqual(["merge:merge", "pr-server:external>https://git.test/server/pull/9937", "ticket:link>/tickets/shop/50", "chain:chain"]);
  });

  it("SeriesTicketActs_NeededAndPrWaiting_MergeDisabledWithItsReason", () => {
    expect(shape("51")).toEqual(["merge:merge:disabled", "pr-server:external>https://git.test/server/pull/9950", "ticket:link>/tickets/shop/51", "chain:chain"]);
    expect(acts("51")[0].disabledBy).toBe("Needed so #62 can start. Merges after #50. Nothing for you to do yet.");
    expect(shape("56")).toEqual(["merge:merge:disabled", "pr-client:external>https://git.test/client/pull/9980", "ticket:link>/tickets/shop/56", "chain:chain"]);
  });

  it("SeriesTicketActs_AnArmedMerge_SaysSoOnTheRow_AndMergeStaysEnabled", () => {
    // 2026-11-24-a4e7: an arm the host dropped would strand the ticket; a second Merge simply arms it again.
    const pr = overviewDetail().tickets.find((candidate) => candidate.ticketId === "50")!.pullRequests[0];
    const armed = with_("50", { pullRequests: [{ ...pr, mergeSaid: { status: "completion_armed", reason: null, at: "2026-11-24T08:00:00Z" } }] });
    const kind = seriesNodeKind(armed.ticket, armed.detail);

    const merge = seriesTicketActs(armed.ticket, armed.detail, kind, false, t)[0];

    expect(seriesNodeSentence(armed.ticket, armed.detail, kind, t)).toContain("The merge is armed: the host completes it once CI is green.");
    expect(merge.id).toBe("merge");
    expect(merge.disabledBy).toBeUndefined();
  });

  // 2026-11-24-f9c5f: a merge run that started a series pull request's CI says so as it is, not as a refusal.
  it("SeriesNodeSentence_ACiStart_IsSaidAsItIs", () => {
    const pr = overviewDetail().tickets.find((candidate) => candidate.ticketId === "50")!.pullRequests[0];
    const said = with_("50", { pullRequests: [{ ...pr, mergeSaid: { status: "completion_refused",
      reason: "CI started: dispatched ci.yml on agent-smith/50", at: "2026-11-24T08:00:00Z" } }] });

    const sentence = seriesNodeSentence(said.ticket, said.detail, seriesNodeKind(said.ticket, said.detail), t);

    expect(sentence).toContain("CI started: dispatched ci.yml on agent-smith/50");
    expect(sentence).not.toContain("The last merge was refused");
  });

  it("SeriesTicketActs_AQuestionOnTheTicket_AnswersOnItsPage_WithTheSeriesConversationBeside", () => {
    expect(shapeOf(with_("61", { hold: hold("question", { act: { kind: "answer", target: null } }) }))).toEqual([
      "answer:link>/tickets/shop/61", "conversation:link>/series/sr-1?section=talk", "ticket:link>/tickets/shop/61", "chain:chain"]);
  });

  it("SeriesTicketActs_RedCi_SeeWhyIsTheCiLog_NoRetry", () => {
    const red = with_("59", { status: "success", hold: hold("ci_failed", { names: ["build"] }),
      pullRequests: [{ repo: "client", number: "9990", url: "https://git.test/client/pull/9990", state: "open", ci: "red",
        ciFailure: "build", ciLog: "https://ci.test/log/1", target: null }] });

    expect(shapeOf(red)).toEqual(["why:external>https://ci.test/log/1", "conversation:link>/series/sr-1?section=talk",
      "pr-client:external>https://git.test/client/pull/9990", "ticket:link>/tickets/shop/59", "chain:chain"]);
  });

  it("SeriesTicketActs_TheHoldsRetryAct_RetriesItsRun_AHandBack_IsNotRetried_ItsConversationIsTheTickets", () => {
    expect(shapeOf(with_("59", { hold: hold("infrastructure", { act: { kind: "retry", target: "run-58" } }) }))[0]).toBe("retry:retry@run-58");
    expect(shapeOf(with_("59", { status: "waiting_for_input", hold: hold("handback", { act: { kind: "conversation", target: null } }) })))
      .toEqual(["why:link>/jobs/run-59/why", "conversation:link>/tickets/shop/59", "ticket:link>/tickets/shop/59", "chain:chain"]);
  });

  it("SeriesTicketActs_AnAdminsHold_LeadsWithItsAct_OnTheTicketPage_NeverRetried", () => {
    expect(shapeOf(with_("59", { hold: hold("image_untrusted", { act: { kind: "settings", target: "/config/settings/sandbox" } }) })))
      .toEqual(["act:link>/tickets/shop/59", "chain:chain"]);
  });

  it("SeriesTicketActs_Question_AnswerIsTheHoldsAct_NoSecondConversation", () => {
    expect(shape("61")).toEqual(["answer:link>/series/sr-1?section=talk", "ticket:link>/tickets/shop/61", "chain:chain"]);
    expect(acts("61")[0].label).toBe("Talk about the plan");
  });

  it("SeriesTicketActs_Failed_RetrySeeWhyConversation", () => {
    // the hold's rework conversation names no series: it is the ticket's own, on its page
    expect(shape("59")).toEqual(["retry:retry", "why:link>/jobs/run-59/why", "conversation:link>/tickets/shop/59",
      "ticket:link>/tickets/shop/59", "chain:chain"]);
  });

  it("SeriesTicketActs_RunningWaitingMerged", () => {
    expect(shape("63")).toEqual(["run:link>/jobs/run-63", "ticket:link>/tickets/shop/63", "chain:chain"]);
    expect(shape("62")).toEqual(["conversation:link>/series/sr-1?section=talk", "ticket:link>/tickets/shop/62", "chain:chain"]);
    expect(shape("48")).toEqual(["pr-server:external>https://git.test/server/pull/9930", "ticket:link>/tickets/shop/48", "chain:chain"]);
  });

  it("SeriesTicketActs_WithoutRunsControl_NoMergeAndNoRetry", () => {
    expect(shape("50", true).some((a) => a.startsWith("merge"))).toBe(false);
    expect(shape("59", true).some((a) => a.startsWith("retry"))).toBe(false);
  });

  it("SeriesTicketActs_TheRowKeepsTheMenusActsOut", () => {
    expect(acts("50").filter((a) => !a.menuOnly).map((a) => a.id)).toEqual(["merge", "pr-server"]);
    expect(acts("59").filter((a) => !a.menuOnly).map((a) => a.id)).toEqual(["retry", "why", "conversation"]);
  });
});

describe("seriesNodeSentence", () => {
  it("SeriesNodeSentence_IsTheHoldsSentence_LedByTheNeededMark_AndADraftSaysSo", () => {
    const detail = overviewDetail();
    const of = (id: string) => { const ticket = detail.tickets.find((c) => c.ticketId === id)!; return seriesNodeSentence(ticket, detail, seriesNodeKind(ticket, detail), t); };

    expect(of("50")).toBe("Its pull request is next in the series' merge order. #51 is next after it. Needed so #62 can start.");
    expect(of("48")).toBe("Merged.");
    expect(of("55")).toMatch(/^Needed so #62 can start\. Merges after #50/);
    detail.tickets[1].pullRequests[0].isDraft = true;
    expect(of("50")).toMatch(/merging takes it out of draft first\.$/);
  });
});
