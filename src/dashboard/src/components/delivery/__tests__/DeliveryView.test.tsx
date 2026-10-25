import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { delivery, figures } from "./deliveryFixtures";

// 2026-10-25-0099c: the Delivery page as the approved mock draws it — eight figures, a timeline row per ticket with
// a bar per attempt, why attempts ended, the project rows and the ticket rows that open their ticket page; the
// chips are the answer's projects.

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push, replace: vi.fn() }), usePathname: () => "/delivery" }));
const fetchDelivery = vi.hoisted(() => vi.fn());
vi.mock("@/lib/deliveryApi", () => ({ fetchDelivery: (...args: unknown[]) => fetchDelivery(...args) }));

const { DeliveryView } = await import("../DeliveryView");

async function shown() {
  render(<DeliveryView client={fakeHub().client} />);
  return screen.findByTestId("delivery-kpis");
}

beforeEach(() => {
  push.mockReset();
  fetchDelivery.mockReset().mockResolvedValue(delivery());
});

describe("DeliveryView", () => {
  it("Renders_TheTilesTimelineCausesProjectsAndTickets", async () => {
    const kpis = await shown();

    expect(within(kpis).getAllByText(/vs last week/)).toHaveLength(8);
    expect(screen.getByTestId("delivery-kpi-Merge rate")).toHaveTextContent("50%");
    expect(screen.getByTestId("delivery-kpi-Tickets worked")).toHaveTextContent("+1");
    const row = screen.getByTestId("delivery-row-GH-102");
    expect(within(row).getAllByRole("button")).toHaveLength(2);
    expect(screen.getByTestId("delivery-bar-GH-102-r2").className).toContain("rework");
    expect(within(screen.getByTestId("delivery-row-GH-101")).getByTestId("delivery-merged")).toBeInTheDocument();
    expect(screen.getByTestId("delivery-cause-preflight")).toHaveTextContent("Preflight failed (build, deps)");
    expect(screen.getByTestId("delivery-project-web-shop")).toHaveTextContent("$12.40");
    const ticket = screen.getByTestId("delivery-ticket-GH-101");
    expect(ticket).toHaveTextContent("Done");
    expect(within(ticket).getByRole("link")).toHaveAttribute("href", "/tickets/web-shop/GH-101");
    fireEvent.click(screen.getByTestId("delivery-ticket-GH-102"));
    expect(push).toHaveBeenCalledWith("/tickets/web-shop/GH-102");
  });

  it("ABar_OnHover_SaysWhenHowLongAndWhy", async () => {
    await shown();

    fireEvent.mouseMove(screen.getByTestId("delivery-bar-GH-102-r1"), { clientX: 10, clientY: 200 });

    expect(screen.getByTestId("delivery-tip")).toHaveTextContent("GH-102 · attempt 1");
    expect(screen.getByTestId("delivery-tip")).toHaveTextContent("Preflight failed (build, deps)");
  });

  it("ANullFigure_ReadsAsADash_AndAnEmptyPeriodSaysSo", async () => {
    fetchDelivery.mockResolvedValue(delivery({
      current: figures({ ticketsWorked: 0, merged: 0, mergeRate: null, medianTimeToMergeSeconds: null }),
      endCauses: [], tickets: [], byProject: [],
    }));
    await shown();

    expect(screen.getByTestId("delivery-kpi-Merge rate")).toHaveTextContent(/^Merge rate-/);
    expect(screen.getByText("No attempt ended in this period.")).toBeInTheDocument();
    expect(screen.getByText("No ticket was worked on in this period.")).toBeInTheDocument();
  });

  it("ATruncatedAnswer_ShowsNoDelta", async () => {
    fetchDelivery.mockResolvedValue(delivery({ truncated: true }));
    await shown();

    expect(screen.getByTestId("delivery-kpi-Tickets worked")).not.toHaveTextContent("+1");
  });

  it("Chips_AreTheAnswersProjects_OffReadsWithout_TheLastStaysOn", async () => {
    await shown();
    expect(screen.getAllByRole("button", { pressed: true }).map((b) => b.textContent)).toEqual(
      expect.arrayContaining(["billing-service", "web-shop"]),
    );

    await act(async () => fireEvent.click(screen.getByTestId("delivery-chip-billing-service")));

    expect(fetchDelivery).toHaveBeenLastCalledWith("week", ["web-shop"], expect.any(AbortSignal));
    expect(screen.getByTestId("delivery-chip-web-shop")).toBeDisabled();
  });

  it("ThePeriodSwitch_ReadsTheChosenPeriod", async () => {
    await shown();

    await act(async () => fireEvent.click(screen.getByTestId("delivery-period-month")));

    expect(fetchDelivery).toHaveBeenLastCalledWith("month", [], expect.any(AbortSignal));
  });
});
