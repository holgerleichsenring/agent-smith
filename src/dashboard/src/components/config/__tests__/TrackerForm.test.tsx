import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import type { CapabilityField, ConfigCapabilities, ConfigFinding, StudioTracker } from "@/lib/configApi";
import { TrackerForm } from "../TrackerForm";
import { TrackerOutcomes } from "../TrackerOutcomes";
import { EntityDrawer } from "../EntityDrawer";
import type { ConfigCatalog } from "../useConfigCatalog";

// 2026-10-06-cea8: the tracker drawer as tabs. Fields are placed by the group the server
// declares; a missing required field is named on itself and marks the tab it lives on.

const validateTrackerDraft = vi.fn();

vi.mock("@/lib/configApi", async (importActual) => ({
  ...(await importActual<typeof import("@/lib/configApi")>()),
  validateTrackerDraft: (...args: unknown[]) => validateTrackerDraft(...args),
}));

const f = (key: string, group: string | null, extra: Partial<CapabilityField> = {}): CapabilityField => ({
  key,
  label: key,
  required: false,
  kind: "text",
  group,
  ...extra,
});

const JIRA: CapabilityField[] = [
  f("url", "connection", { label: "Base URL", required: true }),
  f("authSecret", "connection", { label: "Auth secret", required: true, kind: "secret" }),
  f("email", "connection", { label: "Account email", required: true }),
  f("triggerStatuses", "intake", { kind: "list" }),
  f("doneStatus", "outcome", { label: "Done status" }),
  f("failedStatus", "outcome", { label: "Failed status" }),
  f("needsClarificationStatus", "outcome", { label: "Needs-clarification status" }),
  f("closeTransitionName", "transition"),
  f("pipelineFromLabel", "routing", { kind: "map" }),
  f("workItemKinds", "filing", { kind: "map" }),
];

const GITHUB: CapabilityField[] = JIRA.filter((x) => x.group !== "filing" && x.key !== "email");

const CAPS = {
  trackerTypes: [
    { type: "jira", fields: JIRA },
    { type: "github", fields: GITHUB },
  ],
  connectionTypes: [],
  agentProviders: [],
  resolutionStrategies: [],
  pipelines: [],
  roles: [],
  permissions: [],
  builtInRoles: [],
} as unknown as ConfigCapabilities;

const JIRA_TRACKER = {
  id: "service-one-dev",
  type: "jira",
  url: "https://example.atlassian.net/",
  authSecret: "jira_token",
} as StudioTracker;

const EMAIL_FINDING: ConfigFinding = {
  subsystem: "configuration",
  severity: "blocking",
  reason: "Account email is required.",
  field: "email",
};

function renderForm(draft: StudioTracker, findings: ConfigFinding[] = [], caps = CAPS) {
  return render(
    <TrackerForm
      draft={draft}
      onChange={() => {}}
      capabilities={caps}
      findings={findings}
      secrets={["jira_token"]}
      idField={<span data-testid="id-field" />}
    />,
  );
}

beforeEach(() => {
  validateTrackerDraft.mockReset();
  validateTrackerDraft.mockResolvedValue([]);
});

describe("TrackerForm (2026-10-06-cea8)", () => {
  it("TrackerForm_JiraDescriptor_FieldsOnTheirTabs", () => {
    renderForm(JIRA_TRACKER);
    const expected: Record<string, string[]> = {
      connection: ["url", "authSecret", "email"],
      intake: ["triggerStatuses"],
      outcomes: ["doneStatus", "failedStatus", "needsClarificationStatus", "closeTransitionName"],
      routing: ["pipelineFromLabel"],
      filing: ["workItemKinds"],
    };
    for (const [tab, keys] of Object.entries(expected)) {
      fireEvent.click(screen.getByTestId(`tracker-tab-${tab}`));
      const panel = screen.getByTestId(`tracker-panel-${tab}`);
      for (const key of JIRA.map((x) => x.key)) {
        const shown = panel.querySelector(`[data-testid="form-field-${key}"]`) !== null;
        expect(shown, `${key} on ${tab}`).toBe(keys.includes(key));
      }
    }
  });

  it("TrackerForm_UngroupedField_RendersOnConnection", () => {
    const caps = {
      ...CAPS,
      trackerTypes: [{ type: "jira", fields: [...JIRA, f("brandNew", null)] }],
    } as ConfigCapabilities;
    renderForm(JIRA_TRACKER, [], caps);
    expect(screen.getByTestId("tracker-panel-connection").querySelector('[data-testid="form-field-brandNew"]'))
      .not.toBeNull();
  });

  it("TrackerForm_GithubDescriptor_NoFilingTab", () => {
    renderForm({ ...JIRA_TRACKER, type: "github" } as StudioTracker);
    expect(screen.queryByTestId("tracker-tab-filing")).toBeNull();
    expect(screen.getByTestId("tracker-tab-routing")).toBeTruthy();
  });

  it("TrackerForm_MissingEmail_MarksConnectionTab", () => {
    renderForm(JIRA_TRACKER, [EMAIL_FINDING]);
    expect(screen.getByTestId("tracker-tab-connection").getAttribute("data-marked")).toBe("true");
    expect(screen.getByTestId("tracker-tab-outcomes").getAttribute("data-marked")).toBe("false");
  });
});

describe("TrackerOutcomes (2026-10-06-cea8)", () => {
  const outcomes = JIRA.filter((x) => x.group === "outcome");

  it("TrackerOutcomes_TwoOutcomesShareAStatus_ShowsWarning", () => {
    render(
      <TrackerOutcomes
        fields={outcomes}
        values={{ doneStatus: "In Review", failedStatus: "in review ", needsClarificationStatus: "Blocked" }}
        onFieldChange={() => {}}
        findings={[]}
      />,
    );
    const warning = screen.getByTestId("tracker-outcomes-shared");
    expect(warning.textContent).toContain("Done status and Failed status both move");
    expect(warning.textContent).not.toContain("Needs-clarification");
  });

  it("TrackerOutcomes_AllUnset_NoWarning", () => {
    render(<TrackerOutcomes fields={outcomes} values={{}} onFieldChange={() => {}} findings={[]} />);
    expect(screen.queryByTestId("tracker-outcomes-shared")).toBeNull();
  });
});

describe("EntityDrawer footer (2026-10-06-cea8)", () => {
  it("EntityDrawer_MissingEmail_FooterNamesReasonAndTab", async () => {
    validateTrackerDraft.mockResolvedValue([EMAIL_FINDING]);
    const catalog = { secrets: [{ id: "jira_token" }] } as unknown as ConfigCatalog;
    render(
      <EntityDrawer
        kind="trackers"
        initial={JIRA_TRACKER}
        isNew={false}
        catalog={catalog}
        capabilities={CAPS}
        onClose={() => {}}
        onSaved={() => {}}
      />,
    );
    const footer = await screen.findByTestId("config-drawer-blocked");
    expect(footer.textContent).toBe("Account email is required. (Connection tab)");
  });
});
