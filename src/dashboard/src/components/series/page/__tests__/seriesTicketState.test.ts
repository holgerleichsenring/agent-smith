import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { seriesTicketState } from "../seriesTicketState";
import { seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcd: merged first, then a waiting pull request, then the newest attempt; never a raw status word.

const t = translator("en");

describe("seriesTicketState", () => {
  it.each([
    ["running", "working", "run"],
    ["waiting_for_input", "asks a question", "need"],
    ["failed", "failed", "bad"],
    ["success", "done", "idle"],
    ["cancelled", "cancelled", "idle"],
    ["shortfall", "fell short", "bad"],
    ["something_new", "ended", "idle"],
  ])("seriesTicketState_%s", (status, text, tone) => {
    expect(seriesTicketState(seriesTicket({ status }), t)).toEqual({ text, tone });
  });

  it("seriesTicketState_NoAttempt_IsNotStarted_AndGerman", () => {
    expect(seriesTicketState(seriesTicket(), t).text).toBe("not started");
    expect(seriesTicketState(seriesTicket({ landed: true }), translator("de")).text).toBe("gemergt");
  });
});
