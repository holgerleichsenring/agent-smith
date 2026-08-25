import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { RuntimeSettingsProvider, useRuntimeSettings } from "../RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS, type RuntimeSettings } from "../runtimeSettings";

// 2026-08-25-21ae: what a consumer sees. The provider is the only reader, so
// these cases are the whole contract between it and every surface below it.

function Authority() {
  return <span data-testid="authority">{useRuntimeSettings().auth.authority || "(none)"}</span>;
}

const configured: RuntimeSettings = {
  auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority: "https://login.example.com" },
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("RuntimeSettingsProvider", () => {
  it("Provider_SettingsResolved_EveryConsumerReadsThem", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(
        async () =>
          ({ ok: true, status: 200, json: async () => configured }) as unknown as Response,
      ),
    );

    render(
      <RuntimeSettingsProvider>
        <Authority />
      </RuntimeSettingsProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId("authority")).toHaveTextContent("https://login.example.com"),
    );
  });

  it("Provider_SettingsSupplied_TheFetchIsNotMade", async () => {
    // The seam a test or a story renders through — the same one the event store
    // provider offers, so a surface can be shown configured without a network.
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    render(
      <RuntimeSettingsProvider settings={configured}>
        <Authority />
      </RuntimeSettingsProvider>,
    );

    expect(screen.getByTestId("authority")).toHaveTextContent("https://login.example.com");
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("useRuntimeSettings", () => {
  it("Hook_NoProviderAbove_EveryValueIsItsDefault", () => {
    // A surface rendered outside the shell is unconfigured, not broken.
    render(<Authority />);

    expect(screen.getByTestId("authority")).toHaveTextContent("(none)");
  });
});
