import { act, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { createFakeSource, flush, silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import { EventStore } from "@/lib/eventStore/eventStore";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

// 2026-11-16-0440c: the running system's parts, listed beside the /system pages now that the
// sidebar carries only the work.

const usePathname = vi.fn(() => "/system");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname() }));

import { SystemSectionNav } from "../SystemSectionNav";

function renderNav(pathname: string, store: EventStore = silentEventStore()) {
  usePathname.mockReturnValue(pathname);
  return render(
    <EventStoreProvider store={store}>
      <SystemSectionNav />
    </EventStoreProvider>,
  );
}

beforeEach(() => usePathname.mockReset());

describe("SystemSectionNav", () => {
  it("SystemNav_ListsEverySubsystem_AndTheWayBack", () => {
    renderNav("/system/webhooks");
    for (const href of ["/system/tracker", "/system/webhooks", "/system/chat", "/system/config", "/system/catalog"]) {
      expect(screen.getByTestId("system-nav").querySelector(`a[href='${href}']`)).not.toBeNull();
    }
    expect(screen.getByTestId("system-nav-back")).toHaveAttribute("href", "/");
    expect(screen.getByTestId("section-nav-item-Webhooks")).toHaveAttribute("data-active", "true");
  });

  it("SystemNav_TheBareSystemPath_MarksTheTracker", () => {
    renderNav("/system");
    expect(screen.getByTestId("section-nav-item-Tracker · ticket polling")).toHaveAttribute("data-active", "true");
  });

  it("SystemNav_NoEventYet_EveryEntryShowsTheIdleDot", () => {
    renderNav("/system/chat");
    const dots = screen.getAllByTestId("section-nav-item-dot");
    expect(dots).toHaveLength(5);
    expect(dots[0]).toHaveAttribute("aria-label", "idle");
  });

  // 2026-11-17-d6fbe: restored from the deleted AppRail suite (2026-08-27-1ed6, p0209a) — the Tracker entry names the
  // tracker its newest event came from, and a subsystem with a fresh event shows the live dot and its freshness.
  it("SystemNav_TheTrackerEntry_CarriesTheObservedTrackerName_AndTheLiveDotWithItsFreshness", async () => {
    const fake = createFakeSource();
    renderNav("/system/webhooks", new EventStore(fake.source));
    const event: SystemEvent = {
      source: "poller",
      type: SystemEventType.PollCycleFinished,
      timestamp: new Date().toISOString(),
      tracker: "azdo",
      ticketsPolled: 3,
      matched: 1,
      spawned: 1,
      statusFiltered: 0,
      zeroMatched: 0,
      durationMs: 120,
    };
    await act(async () => {
      fake.emitSystem(event);
      await flush();
    });
    const entry = screen.getByTestId("section-nav-item-Tracker · azdo");
    expect(entry).toHaveAttribute("href", "/system/tracker");
    expect(within(entry).getByTestId("section-nav-item-dot")).toHaveAttribute("aria-label", "live");
    expect(within(entry).getByText("now")).toBeInTheDocument();
    expect(within(screen.getByTestId("section-nav-item-Webhooks")).getByTestId("section-nav-item-dot"))
      .toHaveAttribute("aria-label", "idle");
  });
});
