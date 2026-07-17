import { render, screen } from "@testing-library/react";
import { vi } from "vitest";
import JobsPage from "../page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    // 1 = HubConnectionState.Connected per @microsoft/signalr enum
    connectionState: 1,
    overview: { active: [], recent: [], systemActivity: null },
  }),
}));

// p0343b: the header row hosts the InflowPill, which reads the shared system
// backlog — renders go through an EventStore provider on a silent source.
const renderPage = () =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <JobsPage />
    </EventStoreProvider>,
  );

describe("JobsPage (root /)", () => {
  it("renders the Runs heading", () => {
    renderPage();
    expect(screen.getByRole("heading", { name: /runs/i })).toBeInTheDocument();
  });

  it("mounts mission control (empty state when no runs)", () => {
    renderPage();
    expect(screen.getByTestId("mission-empty")).toBeInTheDocument();
  });

  it("hides the inflow pill when there are no runs at all", () => {
    renderPage();
    expect(screen.queryByTestId("inflow-pill")).not.toBeInTheDocument();
  });
});
