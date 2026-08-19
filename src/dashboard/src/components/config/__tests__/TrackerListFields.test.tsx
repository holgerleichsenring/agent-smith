import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";
import { resetCapabilitiesCache } from "../useCapabilities";

// p0455: the studio talks to a stubbed SERVER here, not to a stubbed configApi — the
// descriptor spells its field shapes the way System.Text.Json writes the C# enum ("List"),
// which is exactly what the module-level fixtures in the other studio tests never did.
// That is why they stayed green while the Edit Tracker dialog showed two empty boxes over
// a tracker that holds ['New','Active'] in both of them.

const TRACKER = {
  id: "sample-tracker",
  type: "azure-devops",
  authSecret: "TRACKER_PAT",
  organization: "sample-org",
  project: "Sample",
  openStates: ["New", "Active"],
  triggerStatuses: ["New", "Active"],
  doneStatus: "Resolved",
};

const CAPABILITIES = {
  trackerTypes: [
    {
      type: "azure-devops",
      fields: [
        { key: "organization", label: "Organization", required: true, kind: "Text" },
        { key: "project", label: "Project", required: true, kind: "Text" },
        { key: "authSecret", label: "Auth secret", required: true, kind: "Text" },
        { key: "triggerStatuses", label: "Trigger statuses", required: false, kind: "List" },
        { key: "openStates", label: "Open states", required: false, kind: "List" },
        { key: "doneStatus", label: "Done status", required: false, kind: "Text" },
      ],
    },
  ],
  connectionTypes: [],
  agentProviders: [],
  resolutionStrategies: [],
  pipelines: [],
  roles: [],
};

let sent: { url: string; method: string; body: unknown }[] = [];

function serve(url: string, init?: RequestInit): Response {
  const method = init?.method ?? "GET";
  if (method !== "GET") sent.push({ url, method, body: JSON.parse(String(init?.body ?? "null")) });
  const ok = (data: unknown) => ({ ok: true, status: 200, json: async () => data }) as Response;
  if (url.endsWith("/api/config/capabilities")) return ok(CAPABILITIES);
  if (url.endsWith("/api/config/trackers") && method === "GET") return ok([TRACKER]);
  if (url.endsWith("/validate")) return ok([]);
  if (method !== "GET") return ok(TRACKER);
  return ok([]);
}

beforeEach(() => {
  sent = [];
  resetCapabilitiesCache();
  vi.stubGlobal("fetch", vi.fn(async (url: string, init?: RequestInit) => serve(url, init)));
});

afterEach(() => vi.unstubAllGlobals());

async function openTrackerDialog(): Promise<HTMLInputElement> {
  render(
    <ConfigCatalogProvider>
      <ConfigStudio section="trackers" />
    </ConfigCatalogProvider>,
  );
  fireEvent.click(await screen.findByTestId("config-card-edit-sample-tracker"));
  return (await screen.findByTestId("form-field-triggerStatuses")) as HTMLInputElement;
}

describe("The tracker dialog shows the lists it holds (p0455)", () => {
  it("AStoredTriggerStatusList_ReachesTheForm", async () => {
    const triggerStatuses = await openTrackerDialog();

    expect(triggerStatuses.value).toBe("New, Active");
    expect((screen.getByTestId("form-field-openStates") as HTMLInputElement).value).toBe("New, Active");
    // The scalar fields never broke; they are here to show the dialog is the same one.
    expect((screen.getByTestId("form-field-doneStatus") as HTMLInputElement).value).toBe("Resolved");
  });

  it("AnUntouchedListRoundTrips_ByteIdentical", async () => {
    await openTrackerDialog();

    fireEvent.click(screen.getByTestId("config-drawer-save"));

    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const put = sent.find((r) => r.method === "PUT")!;
    expect(put.url).toContain("/api/config/trackers/sample-tracker");
    expect(put.body).toEqual(TRACKER);
  });

  it("AClearedListMeansEmpty_NotUnchanged", async () => {
    const triggerStatuses = await openTrackerDialog();

    fireEvent.change(triggerStatuses, { target: { value: "" } });
    fireEvent.click(screen.getByTestId("config-drawer-save"));

    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const put = sent.find((r) => r.method === "PUT")!.body as Record<string, unknown>;
    // The upsert reads an ABSENT list as "keep what is stored", so an emptied list must
    // be present and empty — otherwise clearing it saves as no change at all.
    expect(put.triggerStatuses).toEqual([]);
    expect(put.openStates).toEqual(["New", "Active"]);
  });
});
