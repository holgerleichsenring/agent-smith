import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { DEFAULT_RUNTIME_SETTINGS, RUNTIME_SETTINGS_PATH } from "../runtimeSettings";

// 2026-08-25-21ae: these cases pin the property the whole feature rests on — an
// installation that writes no document behaves exactly as every installation
// behaved before it existed, `next dev` included.

// The boot's answer is memoised in module state, so every case loads the module
// fresh rather than reaching for a reset function that only tests would call.
async function loadFresh() {
  vi.resetModules();
  return await import("../runtimeSettings");
}

function response(init: {
  ok?: boolean;
  status?: number;
  json?: () => Promise<unknown>;
}): Response {
  return {
    ok: init.ok ?? true,
    status: init.status ?? 200,
    json: init.json ?? (async () => ({})),
  } as unknown as Response;
}

beforeEach(() => {
  vi.spyOn(console, "warn").mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("loadRuntimeSettings", () => {
  it("Settings_DocumentAbsent_EveryValueIsItsDefault", async () => {
    // `next dev` runs no entrypoint, so the developer's tree 404s here. That is
    // the OFF state, not a failure.
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => response({ ok: false, status: 404 })),
    );

    const { loadRuntimeSettings } = await loadFresh();

    expect(await loadRuntimeSettings()).toEqual(DEFAULT_RUNTIME_SETTINGS);
  });

  it("Settings_DocumentUnreadable_EveryValueIsItsDefault", async () => {
    // A proxy's HTML error page and a truncated body both arrive as a 200 whose
    // JSON does not parse; a network that never answered rejects outright.
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        response({ json: () => Promise.reject(new SyntaxError("Unexpected token '<'")) }),
      ),
    );

    const { loadRuntimeSettings } = await loadFresh();

    expect(await loadRuntimeSettings()).toEqual(DEFAULT_RUNTIME_SETTINGS);
  });

  it("Settings_NetworkNeverAnswered_EveryValueIsItsDefault", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.reject(new TypeError("Failed to fetch"))),
    );

    const { loadRuntimeSettings } = await loadFresh();

    expect(await loadRuntimeSettings()).toEqual(DEFAULT_RUNTIME_SETTINGS);
  });

  it("Settings_DocumentPresent_ValuesAreTheOnesItCarries", async () => {
    const document = {
      auth: {
        authority: "https://login.example.com/realms/agentsmith",
        clientId: "agentsmith-dashboard",
        audience: "agent-smith",
        scopes: "openid profile",
        redirectPath: "/callback",
      },
    };
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => response({ json: async () => document })),
    );

    const { loadRuntimeSettings } = await loadFresh();

    expect(await loadRuntimeSettings()).toEqual(document);
  });

  it("Settings_DocumentMissesAField_ThatFieldIsItsDefault", async () => {
    // An older entrypoint's document is incomplete, not wrong — one absent field
    // must not cost the browser the four it did write.
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        response({ json: async () => ({ auth: { authority: "https://login.example.com" } }) }),
      ),
    );

    const { loadRuntimeSettings } = await loadFresh();

    expect(await loadRuntimeSettings()).toEqual({
      auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority: "https://login.example.com" },
    });
  });

  it("Settings_Requested_WithCachingDisabled", async () => {
    // Served from the static root, a cached copy would outlive the pod that
    // wrote it and keep answering with the previous installation's settings.
    const fetchMock = vi.fn(async () => response({ json: async () => ({}) }));
    vi.stubGlobal("fetch", fetchMock);

    const { loadRuntimeSettings } = await loadFresh();
    await loadRuntimeSettings();

    expect(fetchMock).toHaveBeenCalledWith(RUNTIME_SETTINGS_PATH, { cache: "no-store" });
  });

  it("Settings_ReadTwice_FetchesOnce", async () => {
    const fetchMock = vi.fn(async () => response({ json: async () => ({}) }));
    vi.stubGlobal("fetch", fetchMock);

    const { loadRuntimeSettings } = await loadFresh();
    const first = await loadRuntimeSettings();
    const second = await loadRuntimeSettings();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(second).toBe(first);
  });
});
