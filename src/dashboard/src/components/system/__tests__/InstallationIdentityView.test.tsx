import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import type { InstallationIdentity } from "@/lib/installationApi";
import { InstallationIdentityView } from "../InstallationIdentityView";

// 2026-08-27-729e: the surface an operator reads instead of opening a container. The
// server states its own build, the agent build per project and the database; the
// dashboard's own release is this bundle's constant, because it cannot reach the server.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

const fetchMock = vi.fn();

const REPORT: InstallationIdentity = {
  serverRelease: "1.2.3",
  serverRevision: "1111111111111111111111111111111111111111",
  agents: [{ project: "alpha", version: "1.2.3", source: "derived" }],
  database: { provider: "sqlite", reachable: true, pendingMigrations: 0, error: null },
};

function serving(report: InstallationIdentity) {
  fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => report });
}

async function shown(testId: string): Promise<HTMLElement> {
  render(<InstallationIdentityView />);
  return waitFor(() => screen.getByTestId(testId));
}

describe("InstallationIdentityView", () => {
  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("Surface_MigrationsPending_SaysHowMany", async () => {
    serving({
      ...REPORT,
      database: { provider: "postgresql", reachable: true, pendingMigrations: 3, error: null },
    });

    const migrations = await shown("installation-migrations");

    expect(migrations.textContent).toContain("3 pending");
    expect(screen.getByTestId("installation-provider").textContent).toContain("postgresql");
  });

  it("Surface_NoAuthorityAndNoMismatch_StillShowsTheVersions", async () => {
    // Nothing is wrong here — no sign-in, no build difference, no pending migration. The
    // versions are readable anyway, which is the entire point: before this they were
    // visible only through a finding, and therefore only when they were wrong.
    serving(REPORT);

    const server = await shown("installation-server");

    expect(server.textContent).toContain("1.2.3");
    expect(screen.getByTestId("installation-agent-alpha").textContent).toContain("1.2.3");
    expect(screen.getByTestId("installation-dashboard")).toBeTruthy();
    expect(screen.getByTestId("installation-migrations").textContent).toContain("up to date");
  });

  it("Surface_AnUnstampedServer_SaysSoInsteadOfShowingNothing", async () => {
    serving({
      serverRelease: null,
      serverRevision: null,
      agents: [{ project: "alpha", version: null, source: "underivable" }],
      database: { provider: "sqlite", reachable: true, pendingMigrations: 0, error: null },
    });

    const server = await shown("installation-server");

    expect(server.textContent).toContain("not stated by this build");
    expect(screen.getByTestId("installation-agent-alpha").textContent).toContain("underivable");
  });

  it("Surface_ADatabaseThatDidNotAnswer_SaysTheCountIsUnknown", async () => {
    serving({
      ...REPORT,
      database: { provider: "sqlite", reachable: false, pendingMigrations: 0, error: "no such host" },
    });

    const migrations = await shown("installation-migrations");

    expect(migrations.textContent).toContain("unknown");
    expect(migrations.textContent).toContain("no such host");
  });
});
