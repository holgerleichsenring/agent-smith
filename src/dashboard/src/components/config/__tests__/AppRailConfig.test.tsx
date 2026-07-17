import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AppRail } from "@/components/shell/AppRail";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// Mirrors the render harness in shell/__tests__/AppRail.test.tsx — the rail reads
// the shared backlog via a provider and a stable hub instance.
const renderRail = () =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <AppRail />
    </EventStoreProvider>,
  );

const usePathname = vi.fn(() => "/");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname() }));

const HUB = {
  client: {
    systemEvents: { add: () => () => {} },
    subscribeSystem: () => Promise.resolve(() => {}),
  },
  connectionState: 1,
  overview: null,
  systemActivity: null,
};
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => HUB }));

beforeEach(() => usePathname.mockReturnValue("/"));

describe("AppRail Configuration", () => {
  it("AppRail_ExposesConfiguration_LinkingToConfigRoute", () => {
    renderRail();
    const item = screen.getByTestId("app-rail-item-Configuration");
    expect(item).toBeInTheDocument();
    expect(item).toHaveAttribute("href", "/config");
  });

  it("AppRail_ConfigurationActive_OnAnyConfigSubroute", () => {
    usePathname.mockReturnValue("/config/projects");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Configuration")).toHaveAttribute("data-active", "true");
  });
});
