import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DegradedBanner } from "../DegradedBanner";
import type { StartupFindings } from "@/lib/findingsApi";

// p0391a: the banner is the operator-visible half of "the server always starts" — a
// degraded server must say what is down without anyone reading container logs.
const findingsRequest = vi.fn();
vi.mock("@/lib/findingsApi", () => ({
  fetchFindings: () => findingsRequest(),
}));

const findings = (over: Partial<StartupFindings> = {}): StartupFindings => ({
  degraded: true,
  blocking: 1,
  advisory: 0,
  findings: [
    {
      subsystem: "configuration",
      severity: "blocking",
      reason: "needs_clarification_status is not set",
      project: "alpha",
      trigger: "github_trigger",
      field: "needs_clarification_status",
    },
  ],
  ...over,
});

describe("DegradedBanner", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock itself as
  // the teardown hook, which then calls it after the test.
  beforeEach(() => {
    findingsRequest.mockReset();
  });

  it("names the unit and the reason of every blocking finding", async () => {
    findingsRequest.mockResolvedValue(findings());

    render(<DegradedBanner />);

    const banner = await screen.findByTestId("degraded-banner");
    expect(banner).toHaveTextContent("configuration / alpha / github_trigger");
    expect(banner).toHaveTextContent("needs_clarification_status is not set");
  });

  it("renders nothing when nothing is wrong", async () => {
    findingsRequest.mockResolvedValue(
      findings({ degraded: false, blocking: 0, findings: [] }),
    );

    render(<DegradedBanner />);

    await waitFor(() => expect(findingsRequest).toHaveBeenCalled());
    expect(screen.queryByTestId("degraded-banner")).toBeNull();
  });

  it("renders nothing when the findings endpoint cannot be reached", async () => {
    findingsRequest.mockImplementation(() => {
      throw new Error("HTTP 500");
    });

    render(<DegradedBanner />);

    await waitFor(() => expect(findingsRequest).toHaveBeenCalled());
    expect(screen.queryByTestId("degraded-banner")).toBeNull();
  });
});
