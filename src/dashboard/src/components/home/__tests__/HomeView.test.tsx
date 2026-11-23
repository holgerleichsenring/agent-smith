import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { hold, list, ticket } from "@/components/tickets/__tests__/ticketFixtures";
import type { TicketListView } from "@/types/tickets";

// 2026-11-17-d6fba: home is input — the composer hands its message to Work it out, Your move shows at most three
// waiting tickets and recent tickets follow, all from the ticket-list read the sidebar counts from.

const hoisted = vi.hoisted(() => ({
  list: null as TicketListView | null,
  push: vi.fn(),
  fetchSpecDialog: vi.fn(),
  admin: false,
}));
vi.mock("@/hooks/useHoldsAdmin", () => ({ useHoldsAdmin: () => hoisted.admin }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: hoisted.push }), usePathname: () => "/" }));
vi.mock("@/lib/tickets/TicketListProvider", () => ({
  useTicketList: () => ({ list: hoisted.list, refusal: null, failed: false, version: 1, reread: () => {} }),
}));
vi.mock("@/lib/specDialogApi", () => ({ fetchSpecDialog: (id: string) => hoisted.fetchSpecDialog(id) }));

const { HomeView } = await import("../HomeView");
const { takeHandover } = await import("@/lib/workItOutHandover");

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();
const waiting = (id: string, minutes: number) =>
  ticket({ ticketId: id, ticketKey: `GH-${id}`, bucket: "needs_you", kind: "handback", since: ago(minutes),
    handback: { text: `question ${id}`, at: ago(minutes) }, hold: hold("handback", { act: { kind: "conversation", target: null } }) });
const project = (name: string) => ({ name, repos: [], templates: [] });

beforeEach(() => {
  hoisted.push.mockReset();
  hoisted.fetchSpecDialog.mockReset().mockResolvedValue({ projects: [project("shop"), project("api")], session: null });
  hoisted.list = list([], { needsYou: 0 });
  hoisted.admin = false;
  takeHandover();
});

describe("HomeView", () => {
  it("Home_AsksWhatToWorkOn_WithTheComposer", async () => {
    render(<HomeView />);
    expect(screen.getByRole("heading", { name: "What should we work on?" })).toBeInTheDocument();
    expect(screen.getByTestId("home-composer")).toBeInTheDocument();
    expect(await screen.findByTestId("home-composer-project")).toHaveValue("shop");
    expect(screen.getByTestId("home-waiting")).toHaveTextContent("Nothing waits on you.");
  });

  it("Home_Send_HandsTheMessageToWorkItOutOnThePickedProject", async () => {
    render(<HomeView />);
    fireEvent.change(await screen.findByTestId("home-composer-project"), { target: { value: "api" } });
    fireEvent.change(screen.getByTestId("dialog-composer-text"), { target: { value: "add a health endpoint" } });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    expect(hoisted.push).toHaveBeenCalledWith("/spec-dialog");
    expect(takeHandover()).toEqual({ text: "add a health endpoint", project: "api", images: [], sets: [] });
  });

  // 2026-11-17-2772: the picker sits in the composer's lower row, attach then project then send, under the text field.
  it("Home_ComposerRow_IsAttachThenProjectThenSend_UnderTheText", async () => {
    render(<HomeView />);
    const picker = await screen.findByRole("combobox", { name: "Project" });
    const row = screen.getByTestId("dialog-composer-tools");
    const attach = screen.getByRole("button", { name: "Attach" });
    const send = screen.getByRole("button", { name: "Send" });
    const text = screen.getByTestId("dialog-composer-text");

    for (const control of [attach, picker, send]) expect(row).toContainElement(control);
    expect(row).not.toContainElement(text);
    const follows = (a: Node, b: Node) => (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
    expect(follows(text, attach)).toBe(true);
    expect(follows(attach, picker)).toBe(true);
    expect(follows(picker, send)).toBe(true);
  });

  it("Home_ComposerRow_ChosenProjectTravelsWithTheMessage", async () => {
    render(<HomeView />);
    fireEvent.change(await screen.findByRole("combobox", { name: "Project" }), { target: { value: "api" } });
    fireEvent.change(screen.getByTestId("dialog-composer-text"), { target: { value: "rename the cart" } });
    fireEvent.keyDown(screen.getByTestId("dialog-composer-text"), { key: "Enter" });

    expect(takeHandover()).toEqual({ text: "rename the cart", project: "api", images: [], sets: [] });
  });

  it("Home_NoProject_CannotSend", async () => {
    hoisted.fetchSpecDialog.mockResolvedValue({ projects: [], session: null });
    render(<HomeView />);
    expect(await screen.findByTestId("home-composer-no-project")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-composer-send")).toBeDisabled();
  });

  it("Home_YourMove_ShowsAtMostThreeLinkedToTheirTicket_AndTheRestOneClickAway", async () => {
    hoisted.list = list([waiting("1", 10), waiting("2", 20), waiting("3", 30), waiting("4", 40), ticket({ ticketId: "9" })], { needsYou: 4 });
    render(<HomeView />);
    const moves = screen.getByTestId("home-your-move");
    expect(moves.querySelectorAll("[data-testid^='home-move-shop-']")).toHaveLength(3);
    expect(screen.getByTestId("home-move-shop-4")).toHaveTextContent("question 4");
    expect(screen.getByTestId("home-move-shop-4").querySelector("a")).toHaveAttribute("href", "/tickets/shop/4");
    expect(screen.getByTestId("home-your-move-all")).toHaveAttribute("href", "/tickets?bucket=needs-you");
    expect(screen.getByTestId("home-waiting")).toHaveTextContent("4 things wait on you.");
    await waitFor(() => expect(hoisted.fetchSpecDialog).toHaveBeenCalled());
  });

  // 2026-11-23-31dcg: Your move is the holds — a merge says its sentence and act; an admin's hold only for an admin.
  it("Home_YourMove_IsTheHolds_AnAdminHoldOnlyForAnAdmin", async () => {
    const merge = ticket({ ticketId: "5", bucket: "needs_you", since: ago(5), hold: hold("merge_next", { act: { kind: "merge", target: "sr-1" } }) });
    const admin = ticket({ ticketId: "6", bucket: "stuck", since: ago(50), hold: hold("image_untrusted", { act: { kind: "settings", target: "/x" } }) });
    hoisted.list = list([merge, admin], { needsYou: 1, adminHolds: 1, stuck: 1 });
    const { unmount } = render(<HomeView />);
    expect(screen.getByTestId("home-move-shop-5")).toHaveTextContent("Its pull request is next in the series' merge order.");
    expect(screen.getByTestId("home-move-shop-5")).toHaveTextContent("Merge");
    expect(screen.queryByTestId("home-move-shop-6")).not.toBeInTheDocument();
    expect(screen.getByTestId("home-waiting")).toHaveTextContent("One thing waits on you.");
    unmount();

    hoisted.admin = true;
    render(<HomeView />);
    expect(screen.getByTestId("home-move-shop-6")).toHaveTextContent("Trust the registry in Settings");
    expect(screen.getByTestId("home-waiting")).toHaveTextContent("2 things wait on you.");
    await waitFor(() => expect(hoisted.fetchSpecDialog).toHaveBeenCalled());
  });

  it("Home_RecentTickets_ListTheOthers", async () => {
    hoisted.list = list([waiting("1", 10), ticket({ ticketId: "9", title: "Fix the export" })], { needsYou: 1 });
    render(<HomeView />);
    expect(screen.getByTestId("home-recent-shop-9")).toHaveTextContent("Fix the export");
    expect(screen.getByTestId("home-recent-shop-9")).toHaveAttribute("href", "/tickets/shop/9");
    expect(screen.queryByTestId("home-recent-shop-1")).not.toBeInTheDocument();
    await waitFor(() => expect(hoisted.fetchSpecDialog).toHaveBeenCalled());
  });
});
