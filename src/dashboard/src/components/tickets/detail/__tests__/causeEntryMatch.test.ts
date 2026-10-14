import { describe, expect, it } from "vitest";
import type { AttemptCause } from "@/types/tickets";
import { causeEntryMatch } from "../causeEntryMatch";
import { entry } from "./detailFixtures";

// 2026-10-13-9a59r: an entry is an attempt's cause on 9a59d's source triple — source, the pull request's
// url (or the ticket's), and the source's own id — never on 9a59h's composite id.

const PR = "https://github.com/acme/portal/pull/813";
const prCause: AttemptCause = {
  channel: "pull_request", actor: "m.reviewer", at: "2026-09-04T14:20:00Z",
  source: { kind: "comment", url: PR, id: "c-77" },
};
const prEntry = entry({
  id: "pr-review:abc:c-77", source: "pr-review", kind: "comment", sourceId: "c-77", prKey: "abc", url: PR, role: "reviewer",
});

describe("causeEntryMatch", () => {
  it("APrComment_MatchesOnSourcePrAndSourceId", () => {
    expect(causeEntryMatch(prEntry, prCause)).toBe(true);
  });

  it("TheSameSourceIdOnAnotherPrOrSource_DoesNotMatch", () => {
    expect(causeEntryMatch({ ...prEntry, url: "https://github.com/acme/portal/pull/812" }, prCause)).toBe(false);
    expect(causeEntryMatch({ ...prEntry, source: "ticket", url: null }, prCause)).toBe(false);
    expect(causeEntryMatch({ ...prEntry, sourceId: "c-78" }, prCause)).toBe(false);
  });

  it("ATicketComment_MatchesItsCommentCause_AndNeverOnTheCompositeId", () => {
    const cause: AttemptCause = { channel: "ticket_comment", source: { kind: "comment", id: "9001" } };
    const comment = entry({ id: "ticket:9001", source: "ticket", kind: "comment", sourceId: "9001", role: "person" });
    expect(causeEntryMatch(comment, cause)).toBe(true);
    expect(causeEntryMatch({ ...comment, sourceId: null }, { ...cause, source: { kind: "comment", id: "ticket:9001" } })).toBe(false);
    expect(causeEntryMatch(comment, { channel: "status_move", source: { kind: "status", detail: "Ready" } })).toBe(false);
  });
});
