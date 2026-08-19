import { describe, it, expect, vi, afterEach } from "vitest";
import { fetchCapabilities, type ConfigCapabilities } from "../configApi";

// p0455: the descriptor's field SHAPE crosses the wire spelled the way the server's own
// enum spells it — "List", "Bool", "Map" — while this client switches on the lowercase
// union. Every studio form reads the shape through this one fetch, so this is where the
// two spellings meet. The payloads below are byte-shaped like the server's response
// (System.Text.Json writes the enum member name), NOT like the hand-written fixtures the
// studio's own tests use — those were green while the screen was blank.

function serverPayload(kind: string): ConfigCapabilities {
  return {
    trackerTypes: [
      {
        type: "azure-devops",
        fields: [
          { key: "triggerStatuses", label: "Trigger statuses", required: false, kind },
          { key: "doneStatus", label: "Done status", required: false, kind: "Text" },
        ],
      },
    ],
    connectionTypes: [
      {
        type: "github",
        orgLabel: "owner",
        fields: [{ key: "extraFields", label: "Extra fields", required: false, kind: "List" }],
      },
    ],
    agentProviders: [],
    resolutionStrategies: [],
    pipelines: [],
    roles: [],
  } as unknown as ConfigCapabilities;
}

function answerWith(payload: ConfigCapabilities): void {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({ ok: true, json: async () => payload } as Response),
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("The capabilities descriptor as the server spells it (p0455)", () => {
  it("AServerCasedFieldKind_ArrivesAsTheKindTheFormsSwitchOn", async () => {
    answerWith(serverPayload("List"));

    const capabilities = await fetchCapabilities();

    const tracker = capabilities.trackerTypes[0].fields;
    expect(tracker[0].kind).toBe("list");
    expect(tracker[1].kind).toBe("text");
    // Connection types are read by the same forms and were blank for the same reason.
    expect(capabilities.connectionTypes[0].fields[0].kind).toBe("list");
    // Nothing else about the field is touched.
    expect(tracker[0]).toEqual({
      key: "triggerStatuses",
      label: "Trigger statuses",
      required: false,
      kind: "list",
    });
  });

  it("AnUnknownFieldKind_StaysWhateverTheServerSaid", async () => {
    answerWith(serverPayload("Duration"));

    const capabilities = await fetchCapabilities();

    // A shape this client cannot edit is not renamed into one it can — the form falls
    // back to a text box, which is the honest answer to "shape unknown".
    expect(capabilities.trackerTypes[0].fields[0].kind).toBe("Duration");
  });
});
