import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { resetLanguage } from "@/lib/i18n/language";
import { delivery } from "./deliveryFixtures";

// 2026-11-23-5669f: the Delivery page in German — its figures, timeline, causes and tables read from the dictionary;
// ticket titles and project names are shown as they came.

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn(), replace: vi.fn() }), usePathname: () => "/delivery" }));
const fetchDelivery = vi.hoisted(() => vi.fn());
vi.mock("@/lib/deliveryApi", () => ({ fetchDelivery: (...args: unknown[]) => fetchDelivery(...args) }));

const { DeliveryView } = await import("../DeliveryView");

describe("Delivery in German", () => {
  it("DeliveryView_German_WordsTheFiguresTimelineAndTables", async () => {
    resetLanguage("de");
    fetchDelivery.mockResolvedValue(delivery());
    render(<DeliveryView client={fakeHub().client} />);
    const kpis = await screen.findByTestId("delivery-kpis");

    expect(within(kpis).getAllByText(/ggü\. letzter Woche/)).toHaveLength(8);
    expect(screen.getByTestId("delivery-kpi-mergeRate")).toHaveTextContent("Merge-Quote50 %");
    expect(screen.getByTestId("delivery-period-week")).toHaveTextContent("Woche");
    expect(screen.getByText("Warum Versuche endeten")).toBeInTheDocument();
    expect(screen.getByTestId("delivery-cause-preflight")).toHaveTextContent("Preflight fehlgeschlagen (Build, Abhängigkeiten)");
    expect(screen.getByTestId("delivery-project-web-shop")).toHaveTextContent("12,40 $");
    expect(within(screen.getByTestId("delivery-row-GH-102")).getByText(/2 Versuche/)).toBeInTheDocument();
    expect(screen.getByTestId("delivery-ticket-GH-101")).toHaveTextContent("2 Specs · 1 Repo · Search pages by cursor");
    expect(screen.getByTestId("delivery-previous")).toHaveAttribute("aria-label", "Vorherige Woche");

    fireEvent.mouseMove(screen.getByTestId("delivery-bar-GH-102-r2"), { clientX: 10, clientY: 200 });
    expect(screen.getByTestId("delivery-tip")).toHaveTextContent("GH-102 · Versuch 2 · Nacharbeit");
    expect(screen.getByTestId("delivery-tip")).toHaveTextContent("läuft");
  });
});
