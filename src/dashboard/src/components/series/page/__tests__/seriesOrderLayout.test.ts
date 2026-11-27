import { describe, expect, it } from "vitest";
import type { SeriesRunEdge } from "@/types/seriesRuns";
import { orthogonalPath, seriesCourse, seriesNodeId, seriesOrderLayout } from "../seriesOrderLayout";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcf: the series graph's layout — a column per depth, a lane per repo in the project's repo order, a
// ticket in more than one repo in its first; the course of a ticket is what it requires and what requires it.

const edge = (ticketId: string, requiredTicketId: string): SeriesRunEdge => ({ ticketId, requiredTicketId });

describe("seriesOrderLayout", () => {
  it("SeriesOrderLayout_SameDepth_SameColumn_LanesInTheProjectsRepoOrder", () => {
    const detail = seriesDetail({
      repos: ["server", "worker", "client"],
      tickets: [
        seriesTicket({ ticketId: "1", repos: ["client"] }),
        seriesTicket({ ticketId: "2", depth: 1, repos: ["server", "worker"] }),
        seriesTicket({ ticketId: "3", depth: 1, repos: ["client"] }),
        seriesTicket({ ticketId: "4", depth: 2, repos: [] }),
      ],
      edges: [edge("2", "1"), edge("3", "1"), edge("4", "3"), edge("4", "9")],
    });

    const layout = seriesOrderLayout(detail);
    const node = (id: string) => layout.nodes.find((n) => n.ticket.ticketId === id)!;

    expect(layout.lanes.map((l) => l.repo), "worker has no ticket of its own; no repo last").toEqual(["server", "client", null]);
    expect(node("2").x).toBe(node("3").x);
    expect(node("1").x).toBeLessThan(node("2").x);
    expect(node("2").lane).toBe("server");
    expect(node("2").y).toBeLessThan(node("3").y);
    expect(node("4").lane).toBeNull();
    expect(layout.lines.map((l) => `${l.edge.requiredTicketId}>${l.edge.ticketId}`), "9 is not drawn").toEqual(["1>2", "1>3", "3>4"]);
  });

  // 2026-11-27-ecf2b: a ticket with pull requests in three repos sits on its spec's repo, not its first pull request's.
  it("SeriesOrderLayout_ALaneRepo_PutsTheNodeThere_NotOnItsFirstPullRequestsRepo", () => {
    const detail = seriesDetail({
      repos: ["worker", "client", "server"],
      tickets: [seriesTicket({ ticketId: "1", repos: ["worker", "client", "server"], laneRepo: "server" }),
        seriesTicket({ ticketId: "2", repos: ["worker"], laneRepo: null })],
      edges: [],
    });

    const layout = seriesOrderLayout(detail);

    expect(layout.nodes.map((n) => n.lane)).toEqual(["server", null]);
    expect(layout.lanes.map((l) => l.repo)).toEqual(["server", null]);
  });

  it("SeriesOrderLayout_TwoTicketsInOneLaneAndColumn_StackInSeriesOrder", () => {
    const layout = seriesOrderLayout(seriesDetail({
      tickets: [seriesTicket({ ticketId: "1" }), seriesTicket({ ticketId: "2" })], edges: [],
    }));

    expect(layout.nodes[0].x).toBe(layout.nodes[1].x);
    expect(layout.nodes[1].y).toBeGreaterThan(layout.nodes[0].y);
  });
});

// 2026-11-23-f562: edges run orthogonally with slightly rounded corners — no cubic curves.
describe("orthogonalPath", () => {
  it("OrthogonalPath_LevelEnds_IsAStraightLine", () => {
    expect(orthogonalPath(10, 20, 100, 20)).toBe("M10,20 H100");
  });

  it("OrthogonalPath_BendsJustBeforeTheTarget_WithRoundedCorners_AndNoCurve", () => {
    const d = orthogonalPath(166, 23, 192, 105);

    expect(d).toBe("M166,23 H174 Q180,23 180,29 V99 Q180,105 186,105 H192");
    expect(d).not.toMatch(/C/);
  });

  it("OrthogonalPath_Upwards_RoundsTheOtherWay", () => {
    expect(orthogonalPath(0, 100, 50, 40)).toBe("M0,100 H32 Q38,100 38,94 V46 Q38,40 44,40 H50");
  });

  it("SeriesOrderLayout_EveryLine_IsOrthogonal", () => {
    const layout = seriesOrderLayout(seriesDetail({
      tickets: [seriesTicket({ ticketId: "1" }), seriesTicket({ ticketId: "2", depth: 1, repos: ["worker"] })], repos: ["server", "worker"],
      edges: [edge("2", "1")],
    }));

    expect(layout.lines).toHaveLength(1);
    expect(layout.lines[0].d).toMatch(/^M[\d.]+,[\d.]+ H[\d.]+ Q/);
  });
});

describe("seriesNodeId", () => {
  it("SeriesNodeId_IsAValidDomId_ForAnyTicketId", () => {
    expect(seriesNodeId("SCRUM-68")).toBe("sr-node-SCRUM-68");
    expect(seriesNodeId("a b/c")).toBe("sr-node-a_32_b_47_c");
    expect(seriesNodeId("a.b")).not.toBe(seriesNodeId("a_b"));
  });
});

describe("seriesCourse", () => {
  it("SeriesCourse_IsTheTicketsUpstreamAndDownstream_NotItsSiblings", () => {
    const edges = [edge("2", "1"), edge("3", "2"), edge("4", "1"), edge("5", "3")];

    expect([...seriesCourse(edges, "2")].sort()).toEqual(["1", "2", "3", "5"]);
  });
});
