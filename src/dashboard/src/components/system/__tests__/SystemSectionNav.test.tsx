import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// 2026-11-16-0440c: the running system's parts, listed beside the /system pages now that the
// sidebar carries only the work.

const usePathname = vi.fn(() => "/system");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname() }));

import { SystemSectionNav } from "../SystemSectionNav";

function renderNav(pathname: string) {
  usePathname.mockReturnValue(pathname);
  return render(
    <EventStoreProvider store={silentEventStore()}>
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
});
