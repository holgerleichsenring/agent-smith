import { describe, expect, it } from "vitest";
import type { SeriesRunEdge } from "@/types/seriesRuns";
import { seriesCourse, seriesOrderLayout } from "../seriesOrderLayout";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcf: the Order and branches layout — a column per depth, a lane per repo in the project's repo order, a
// ticket in more than one repo in its first; the course of a ticket is what it requires and what requires it.

const edge = (ticketId: string, requiredTicketId: string, kind: SeriesRunEdge["kind"] = "stacked"): SeriesRunEdge =>
  ({ ticketId, requiredTicketId, kind });

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
      edges: [edge("2", "1", "merge"), edge("3", "1"), edge("4", "3", "unknown"), edge("4", "9")],
    });

    const layout = seriesOrderLayout(detail);
    const node = (id: string) => layout.nodes.find((n) => n.ticket.ticketId === id)!;

    expect(layout.lanes.map((l) => l.repo), "worker has no ticket of its own; no repo last").toEqual(["server", "client", null]);
    expect(node("2").x).toBe(node("3").x);
    expect(node("1").x).toBeLessThan(node("2").x);
    expect(node("2").lane).toBe("server");
    expect(node("2").alsoIn).toEqual(["worker"]);
    expect(node("2").y).toBeLessThan(node("3").y);
    expect(node("4").lane).toBeNull();
    expect(layout.lines.map((l) => `${l.edge.requiredTicketId}>${l.edge.ticketId}`), "9 is not drawn").toEqual(["1>2", "1>3", "3>4"]);
  });

  it("SeriesOrderLayout_TwoTicketsInOneLaneAndColumn_StackInSeriesOrder", () => {
    const layout = seriesOrderLayout(seriesDetail({
      tickets: [seriesTicket({ ticketId: "1" }), seriesTicket({ ticketId: "2" })], edges: [],
    }));

    expect(layout.nodes[0].x).toBe(layout.nodes[1].x);
    expect(layout.nodes[1].y).toBeGreaterThan(layout.nodes[0].y);
  });
});

describe("seriesCourse", () => {
  it("SeriesCourse_IsTheTicketsUpstreamAndDownstream_NotItsSiblings", () => {
    const edges = [edge("2", "1"), edge("3", "2"), edge("4", "1"), edge("5", "3")];

    expect([...seriesCourse(edges, "2")].sort()).toEqual(["1", "2", "3", "5"]);
  });
});
