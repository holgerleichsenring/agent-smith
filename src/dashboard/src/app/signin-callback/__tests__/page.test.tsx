import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import SignInCallbackPage from "../page";

// 2026-08-25-2de1: the route redirectPath names. It completes nothing itself —
// the boot owns the exchange — so what it owes the person is the way back, and a
// visible reason when the authority refused.

const replace = vi.hoisted(() => vi.fn());
const boot = vi.hoisted(() => ({
  session: null as { returnTo: string; error: string | null; isSilentReturn?: boolean } | null,
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

vi.mock("@/lib/auth/session", () => ({
  startAuthSession: async () => boot.session,
}));

beforeEach(() => {
  replace.mockReset();
  boot.session = null;
});

describe("SignInCallbackPage", () => {
  it("Callback_TheExchangeSucceeded_ReturnsToWhereThePersonWas", async () => {
    boot.session = { returnTo: "/jobs/run-1", error: null };

    render(<SignInCallbackPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/jobs/run-1"));
  });

  it("Callback_NoAuthorityConfigured_LandsOnTheDashboardRatherThanNowhere", async () => {
    render(<SignInCallbackPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/"));
  });

  // 2026-08-28-0f46: the hidden frame of a silent sign-in loads THIS route — the
  // library points it at the ordinary reply URL and this phase registers no
  // second one. Its answer has already been posted to the window that asked, so
  // routing it would only boot a second copy of the dashboard inside a document
  // about to be removed, inside the ten seconds that window is counting.
  it("Callback_ASilentReturn_NavigatesNowhereAtAll", async () => {
    boot.session = { returnTo: "/jobs", error: null, isSilentReturn: true };

    render(<SignInCallbackPage />);
    await waitFor(() => expect(screen.getByText(/Signing in/)).toBeInTheDocument());

    expect(replace).not.toHaveBeenCalled();
  });

  it("Callback_TheAuthorityRefused_SaysSoAndStaysPut", async () => {
    boot.session = { returnTo: "/jobs", error: "access_denied" };

    render(<SignInCallbackPage />);

    expect(await screen.findByText(/access_denied/)).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
});
