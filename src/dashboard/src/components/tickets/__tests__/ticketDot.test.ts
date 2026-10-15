import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { TICKET_DOT_CLASSES, ticketDot } from "../ticketDot";
import { pullRequestFate } from "../ticketFormat";
import { pr, ticket } from "./ticketFixtures";

const css = readFileSync("src/styles/mock-parity.css", "utf8");
const rule = (selector: string) => css.match(new RegExp(`${selector.replace(/\./g, "\\.")} \\{([^}]*)\\}`))?.[1];

describe("ticketDot", () => {
  it("maps every bucket and landing to one class", () => {
    const cases: Array<[Parameters<typeof ticketDot>[0], string]> = [
      [ticket({ bucket: "needs_you" }), "need"],
      [ticket({ bucket: "working", status: "running" }), "run"],
      [ticket({ bucket: "working", status: "queued" }), "queued"],
      [ticket({ bucket: "waiting_on_review" }), "human"],
      [ticket({ bucket: "stuck" }), "bad"],
      [ticket({ bucket: "done", lastOutcome: null, landing: "merged" }), "ok"],
      [ticket({ bucket: "idle", lastOutcome: "success", landing: "unmerged" }), "bad"],
      [ticket({ bucket: "idle", lastOutcome: "success" }), "ok"],
      [ticket({ bucket: "idle", lastOutcome: "shortfall" }), "ok"],
      [ticket({ bucket: "idle", lastOutcome: "failed" }), "bad"],
      [ticket({ bucket: "idle", lastOutcome: "cancelled" }), "bad"],
      [ticket({ bucket: "idle", lastOutcome: null }), "idle"],
      [ticket({ bucket: "blocked" }), "blocked"],
      [ticket({ bucket: "filed" }), "filed"],
    ];
    expect(cases.map(([t]) => ticketDot(t))).toEqual(cases.map(([, cls]) => cls));
  });

  it("every class it can return has a rule under .sd and .ticon", () => {
    for (const cls of TICKET_DOT_CLASSES) {
      expect(rule(`.mock-tickets .sd.${cls}`), `.sd.${cls}`).toBeDefined();
      expect(rule(`.mock-tickets .t-head .ticon.${cls}`), `.ticon.${cls}`).toBeDefined();
    }
    expect(rule(".mock-tickets .t-head .ticon.person")).toBeUndefined();
  });

  it("only run and need pulse on the header; every other .ticon class sets animation:none", () => {
    for (const cls of TICKET_DOT_CLASSES) {
      const pulses = !/animation: ?none/.test(rule(`.mock-tickets .t-head .ticon.${cls}`) ?? "");
      expect(pulses, `.ticon.${cls}`).toBe(cls === "run" || cls === "need");
    }
  });
});

describe("pullRequestFate", () => {
  it("counts merged, open and closed unmerged in that order and leaves unsupported and unread out", () => {
    expect(pullRequestFate([pr({ state: "merged" }), pr({ state: "merged" })])).toBe("2 PRs merged");
    expect(pullRequestFate([pr({ state: "open" }), pr({ state: "unread" }), pr({ state: "merged" })])).toBe("1 PR merged, 1 open");
    expect(pullRequestFate([pr({ state: "closed" }), pr({ state: "closed" }), pr({ state: "closed" })])).toBe("3 PRs closed unmerged");
    expect(pullRequestFate([pr({ state: "unsupported" })])).toBeNull();
    expect(pullRequestFate([])).toBeNull();
  });
});
