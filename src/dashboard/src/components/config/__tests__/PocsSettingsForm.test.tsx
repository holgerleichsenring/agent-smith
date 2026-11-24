import { useState } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiResponseError } from "@/lib/apiResponse";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PocsSetting, SettingValue, StudioEntity } from "@/lib/configApi";
import * as configApi from "@/lib/configApi";
import * as pocsApi from "@/lib/pocsApi";
import {
  POC_ARCHIVE_DEFAULTS, POC_GITOPS_DEFAULTS, POC_SCAN_DEFAULTS, POC_VARIABLES_DEFAULTS, PocsSettingsForm, type PocsCatalog,
} from "../PocsSettingsForm";
import { newPocDeployBlocks } from "../PocDeployFields";
import { POC_BUILD_DEFAULTS } from "../PocBuildFields";
import { newPocSecrets } from "../PocSecretsFields";

// 2026-11-23-933da: the PoCs settings form edits the switch, the poc- projects' agent and tracker, and the targets,
// and what it hands back is the wire shape the settings route saves.

const entity = (id: string) => ({ id }) as StudioEntity;
const CATALOG: PocsCatalog = {
  agents: [entity("claude")], trackers: [entity("jira")], repos: [entity("poc-source")], connections: [entity("github")],
  secrets: [entity("poc_pull_token")],
};
const OFF: PocsSetting = {
  enabled: false, projectAgent: null, projectTracker: null, targets: [], archive: POC_ARCHIVE_DEFAULTS,
  scan: POC_SCAN_DEFAULTS, blockedRetentionDays: 7, variables: POC_VARIABLES_DEFAULTS, gitops: POC_GITOPS_DEFAULTS,
};

function Harness({ seen, start = OFF }: { seen: SettingValue[]; start?: PocsSetting }) {
  const [value, setValue] = useState<PocsSetting>(start);
  return (
    <PocsSettingsForm value={value} catalog={CATALOG}
      onChange={(next) => {
        seen.push(next);
        setValue(next as PocsSetting);
      }} />
  );
}

describe("PocsSettingsForm", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("PocsSettingsForm_SwitchAndATarget_RoundTripToTheWireShape", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));
    fireEvent.change(screen.getByTestId("setting-pocs-agent"), { target: { value: "claude" } });
    fireEvent.change(screen.getByTestId("setting-pocs-tracker"), { target: { value: "jira" } });
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-name"), { target: { value: "main" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-source"), { target: { value: "github/poc-source" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-domain"), { target: { value: "{app}.poc.example.internal" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-reminders"), { target: { value: "10, 2" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved).toEqual({
      enabled: true, projectAgent: "claude", projectTracker: "jira",
      targets: [{
        name: "main", sourceRepo: "github/poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
        domainPattern: "{app}.poc.example.internal", lifetimeDays: 90, reminderDays: [10, 2], perPersonLimit: 3,
        registry: {
          type: "acr", loginServer: "", repository: "pocs", pullUser: "", pullAuth: "", pullMode: "pull_secret",
        },
        build: POC_BUILD_DEFAULTS,
        secrets: { provider: "sealed_secrets", sealedSecretsCert: null, sealedSecretsCertUrl: null },
        ingress: { class: null, tlsIssuer: null },
        auth: { ingressAnnotations: {} },
        network: {
          ingressControllerNamespace: null, dnsNamespace: "kube-system", dnsPodLabels: { "k8s-app": "kube-dns" },
          egressProxy: {
            url: null, port: 3128, namespace: null, podLabels: {}, cidr: null, noProxy: "localhost,127.0.0.1,.svc,.cluster.local",
          },
        },
        quota: { cpu: "1", memory: "1Gi", pods: 4, ephemeralStorage: "2Gi" },
        container: { cpuRequest: "50m", cpuLimit: "500m", memoryRequest: "128Mi", memoryLimit: "512Mi", ephemeralLimit: "256Mi" },
      }],
      archive: POC_ARCHIVE_DEFAULTS,
      scan: POC_SCAN_DEFAULTS,
      blockedRetentionDays: 7,
      variables: { maxNames: 64, maxValueKb: 4 },
      gitops: { commitAttempts: 3, maxFolderFiles: 500 },
    });
  });

  it("PocsSettingsForm_NewTarget_HasNoAddressDefault_OnlyAPlaceholder_AndCanBeRemoved", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));

    expect(screen.getByTestId("setting-pocs-target-0-domain")).toHaveValue("");
    expect(screen.getByTestId("setting-pocs-target-0-domain")).toHaveAttribute("placeholder", "{app}.poc.example.internal");

    fireEvent.click(screen.getByTestId("setting-pocs-target-0-remove"));
    expect((seen[seen.length - 1] as PocsSetting).targets).toEqual([]);
    expect(screen.getByTestId("setting-pocs-no-target")).toBeInTheDocument();
  });

  // 2026-11-23-933db: the archive bounds are ten number fields; each edit round-trips, and a value read without archive
  // is given the defaults and sends them back, because a save without archive would reset the limits.
  it("PocsSettingsForm_ArchiveFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-archive-toggle"));

    expect(screen.getByTestId("setting-pocs-archive-maxZipMb")).toHaveValue(25);
    expect(screen.getByTestId("setting-pocs-archive-mismatchFileLimit")).toHaveValue(100);
    fireEvent.change(screen.getByTestId("setting-pocs-archive-maxZipMb"), { target: { value: "40" } });
    fireEvent.change(screen.getByTestId("setting-pocs-archive-mismatchRatioPercent"), { target: { value: "70" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.archive).toEqual({ ...POC_ARCHIVE_DEFAULTS, maxZipMb: 40, mismatchRatioPercent: 70 });
  });

  it("PocsSettingsForm_ValueWithoutArchive_SendsTheDefaultsBack", () => {
    const seen: SettingValue[] = [];
    const legacy = { enabled: false, projectAgent: null, projectTracker: null, targets: [] } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));

    expect((seen[seen.length - 1] as PocsSetting).archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  // 2026-11-23-933dc: the scan fields and the blocked-upload retention round-trip; the secret line is fixed text; a value
  // read without scan or retention gets the defaults and sends them back, beside archive.
  it("PocsSettingsForm_ScanFields_EditAndRoundTrip_WithTheFixedSecretLine", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-scan-toggle"));

    expect(screen.getByTestId("setting-pocs-scan-secret-fixed")).toHaveTextContent("A blocking secret cannot be overruled.");
    expect(screen.getByTestId("setting-pocs-scan-osvImage")).toHaveValue("");
    expect(screen.getByTestId("setting-pocs-scan-timeoutSeconds")).toHaveValue(300);
    expect(screen.getByTestId("setting-pocs-scan-secretMinConfidence")).toHaveValue(8);
    expect(screen.getByTestId("setting-pocs-blockedRetentionDays")).toHaveValue(7);

    const image = "ghcr.io/google/osv-scanner@sha256:" + "a".repeat(64);
    fireEvent.change(screen.getByTestId("setting-pocs-scan-osvImage"), { target: { value: image } });
    fireEvent.change(screen.getByTestId("setting-pocs-scan-timeoutSeconds"), { target: { value: "600" } });
    fireEvent.click(screen.getByTestId("setting-pocs-scan-unfixedCriticalBlock"));
    fireEvent.change(screen.getByTestId("setting-pocs-scan-secretMinConfidence"), { target: { value: "9" } });
    fireEvent.change(screen.getByTestId("setting-pocs-blockedRetentionDays"), { target: { value: "14" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.scan).toEqual({ osvImage: image, timeoutSeconds: 600, unfixedCriticalBlock: false, secretMinConfidence: 9, trivyImage: "" });
    expect(saved.blockedRetentionDays).toBe(14);
    expect(saved.archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  it("PocsSettingsForm_ValueWithoutScan_SendsScanAndRetentionDefaultsBack", () => {
    const seen: SettingValue[] = [];
    const legacy = {
      enabled: false, projectAgent: null, projectTracker: null, targets: [], archive: POC_ARCHIVE_DEFAULTS,
    } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));

    const saved = seen[seen.length - 1] as PocsSetting;
    expect(saved.scan).toEqual(POC_SCAN_DEFAULTS);
    expect(saved.blockedRetentionDays).toBe(7);
    expect(saved.archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  // 2026-11-23-933de: the registry section saves the pull token's secret id and the pull mode, and Test connection checks
  // the values in the form, showing the server's steps.
  it("PocsSettingsForm_Registry_SavesPullTokenAndMode_AndChecksTheFormValues", async () => {
    const check = vi.spyOn(configApi, "checkPocRegistryDraft").mockResolvedValue({
      ok: true,
      steps: [
        { key: "secret", label: "Secret", ok: true, detail: "Secret 'poc_pull_token' has a value." },
        { key: "scope", label: "Scope", ok: true, detail: "The pull token reaches pocregistry.azurecr.io/pocs." },
      ],
    });
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    const id = "setting-pocs-target-0-registry";

    fireEvent.change(screen.getByTestId(`${id}-login-server`), { target: { value: "pocregistry.azurecr.io" } });
    fireEvent.change(screen.getByTestId(`${id}-pull-user`), { target: { value: "poc-pull" } });
    fireEvent.change(screen.getByTestId(`${id}-pull-auth`), { target: { value: "poc_pull_token" } });
    fireEvent.click(screen.getByTestId(`${id}-pull-mode-cluster_pulls`));

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    const registry = {
      type: "acr", loginServer: "pocregistry.azurecr.io", repository: "pocs", pullUser: "poc-pull",
      pullAuth: "poc_pull_token", pullMode: "cluster_pulls",
    };
    expect(saved.targets[0].registry).toEqual(registry);

    fireEvent.click(screen.getByTestId(`${id}-check-run`));
    expect(check).toHaveBeenCalledWith(registry, expect.anything());
    expect(await screen.findByTestId(`${id}-check-step-scope`)).toHaveTextContent(
      "The pull token reaches pocregistry.azurecr.io/pocs.");
  });

  // 2026-11-23-933dd: the Trivy image, the build block of a target and the Install action.
  it("PocsSettingsForm_TrivyImageAndBuildFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-scan-toggle"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));

    const trivy = "aquasec/trivy@sha256:" + "b".repeat(64);
    fireEvent.change(screen.getByTestId("setting-pocs-scan-trivyImage"), { target: { value: trivy } });
    expect(screen.getByTestId("setting-pocs-target-0-build-buildTimeoutMinutes")).toHaveValue(12);
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-build-buildTimeoutMinutes"), { target: { value: "15" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-build-githubEnvironment"), { target: { value: "registry-push" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.scan.trivyImage).toBe(trivy);
    expect(saved.targets[0].build).toEqual({ ...POC_BUILD_DEFAULTS, buildTimeoutMinutes: 15, githubEnvironment: "registry-push" });
  });

  it("PocsSettingsForm_Install_IsOffWhileThePocsSwitchIs_AndSaysTheRunsOutcome", async () => {
    const install = vi.spyOn(pocsApi, "installPocTarget").mockResolvedValue({
      target: "main", succeeded: true, runId: "r1", message: "The build pipeline of target 'main' is commit abc123.",
    });
    const target = { name: "main", sourceRepo: "poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
      domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3, registry: null };
    const { unmount } = render(<Harness seen={[]} start={{ ...OFF, targets: [target] }} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));
    expect(screen.getByTestId("setting-pocs-target-0-build-install")).toBeDisabled();
    expect(screen.getByTestId("setting-pocs-target-0-build-install-said")).toHaveTextContent("Turn PoCs on to install.");
    unmount();

    render(<Harness seen={[]} start={{ ...OFF, enabled: true, targets: [target] }} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-install"));

    expect(install).toHaveBeenCalledWith("main");
    expect(await screen.findByText("The build pipeline of target 'main' is commit abc123.")).toBeInTheDocument();
  });

  // 2026-11-23-933dg: the variables bounds and each target's secrets block are always sent back; a value read without
  // them gets the defaults. The image fields show the server's pinned defaults as placeholders.
  it("PocsSettingsForm_Variables_EditAndRoundTrip_AndALegacyValueSendsTheDefaultsBack", () => {
    const seen: SettingValue[] = [];
    const target = { name: "main", sourceRepo: "poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
      domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3, registry: null };
    const legacy = {
      enabled: false, projectAgent: null, projectTracker: null, targets: [target], archive: POC_ARCHIVE_DEFAULTS,
      scan: POC_SCAN_DEFAULTS, blockedRetentionDays: 7,
    } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));
    let saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.variables).toEqual({ maxNames: 64, maxValueKb: 4 });
    expect(saved.targets[0].secrets).toEqual(newPocSecrets());

    fireEvent.click(screen.getByTestId("setting-pocs-variables-toggle"));
    expect(screen.getByTestId("setting-pocs-variables-maxNames")).toHaveValue(64);
    fireEvent.change(screen.getByTestId("setting-pocs-variables-maxNames"), { target: { value: "32" } });
    fireEvent.change(screen.getByTestId("setting-pocs-variables-maxValueKb"), { target: { value: "8" } });
    saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.variables).toEqual({ maxNames: 32, maxValueKb: 8 });
    expect(screen.getByTestId("setting-pocs-variables")).toHaveTextContent("a Kubernetes Secret or ConfigMap holds at most 1 MiB");
  });

  it("PocsSettingsForm_ScanImages_ShowTheServerDefaultsAsPlaceholders", () => {
    render(<Harness seen={[]} />);
    fireEvent.click(screen.getByTestId("setting-pocs-scan-toggle"));

    expect(screen.getByTestId("setting-pocs-scan-osvImage")).toHaveAttribute("placeholder",
      "ghcr.io/google/osv-scanner:v2.2.4@sha256:f7ba4be68bac8086b1f88fd598fdca1ca67239c79ad2c2b5c78e03a82e5187c4");
    expect(screen.getByTestId("setting-pocs-scan-trivyImage")).toHaveAttribute("placeholder",
      "aquasec/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb");
    expect(screen.getByTestId("setting-pocs-scan-osvImage")).toHaveValue("");
  });

  it("PocsSettingsForm_Certificate_ShowsFingerprintExpiryAndAge_FromTheInspectAnswer", async () => {
    const issued = new Date(Date.now() - 12 * 24 * 60 * 60 * 1000 - 60_000).toISOString();
    const inspect = vi.spyOn(configApi, "fetchSealedSecretsCert").mockResolvedValue({
      pem: "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----", fingerprint: "ab:cd:ef",
      notBefore: issued, notAfter: "2036-11-20T00:00:00Z",
    });
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    const id = "setting-pocs-target-0-secrets";
    fireEvent.click(screen.getByTestId(`${id}-toggle`));
    expect(screen.getByTestId(`${id}-provider`)).toHaveValue("sealed_secrets");

    const pem = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----";
    fireEvent.change(screen.getByTestId(`${id}-cert`), { target: { value: pem } });

    expect(await screen.findByTestId(`${id}-fingerprint`)).toHaveTextContent("ab:cd:ef");
    expect(inspect).toHaveBeenCalledWith({ pem }, expect.anything());
    expect(screen.getByTestId(`${id}-not-after`)).toHaveTextContent("Valid until 2036-11-20");
    expect(screen.getByTestId(`${id}-age`)).toHaveTextContent("Issued 12 days ago");
    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.targets[0].secrets).toEqual({ provider: "sealed_secrets", sealedSecretsCert: pem, sealedSecretsCertUrl: null });
  });

  it("PocsSettingsForm_CertificateFetch_PutsThePemInTheField_AndARefusalIsShownAsSaid", async () => {
    const pem = "-----BEGIN CERTIFICATE-----\nMIIC\n-----END CERTIFICATE-----";
    const fetchCert = vi.spyOn(configApi, "fetchSealedSecretsCert")
      .mockRejectedValueOnce(new ApiResponseError("/api/config/pocs/sealed-secrets-cert/fetch", 502, "HTTP 502",
        "https://certs.example.internal/v1/cert.pem did not answer within 10 seconds."))
      .mockResolvedValue({ pem, fingerprint: "12:34", notBefore: "2026-01-01T00:00:00Z", notAfter: "2026-02-01T00:00:00Z" });
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    const id = "setting-pocs-target-0-secrets";
    fireEvent.click(screen.getByTestId(`${id}-toggle`));
    expect(screen.getByTestId(`${id}-fetch`)).toBeDisabled();

    const url = "https://certs.example.internal/v1/cert.pem";
    fireEvent.change(screen.getByTestId(`${id}-cert-url`), { target: { value: url } });
    fireEvent.click(screen.getByTestId(`${id}-fetch`));
    expect(await screen.findByTestId(`${id}-fetch-error`)).toHaveTextContent("did not answer within 10 seconds.");

    fireEvent.click(screen.getByTestId(`${id}-fetch`));
    await waitFor(() => expect(screen.getByTestId(`${id}-cert`)).toHaveValue(pem));
    expect(fetchCert).toHaveBeenCalledWith({ url });
    expect(screen.getByTestId(`${id}-not-after`)).toHaveTextContent("Expired on 2026-02-01");
    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.targets[0].secrets).toEqual({ provider: "sealed_secrets", sealedSecretsCert: pem, sealedSecretsCertUrl: url });
  });

  // 2026-11-23-c81eb: the GitOps commit bounds and each target's ingress, auth, network, quota and container blocks.
  it("PocsSettingsForm_GitOpsFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-gitops-toggle"));

    expect(screen.getByTestId("setting-pocs-gitops-commitAttempts")).toHaveValue(3);
    expect(screen.getByTestId("setting-pocs-gitops-maxFolderFiles")).toHaveValue(500);
    fireEvent.change(screen.getByTestId("setting-pocs-gitops-commitAttempts"), { target: { value: "5" } });
    fireEvent.change(screen.getByTestId("setting-pocs-gitops-maxFolderFiles"), { target: { value: "200" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.gitops).toEqual({ commitAttempts: 5, maxFolderFiles: 200 });
    expect(saved.archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  it("PocsSettingsForm_DeployFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    const id = "setting-pocs-target-0";
    for (const section of ["ingress", "auth", "network", "quota", "container"])
      fireEvent.click(screen.getByTestId(`${id}-${section}-toggle`));

    fireEvent.change(screen.getByTestId(`${id}-ingress-class`), { target: { value: "nginx" } });
    fireEvent.click(screen.getByTestId(`${id}-auth-annotation-add`));
    fireEvent.change(screen.getByTestId(`${id}-auth-annotation-0-name`), { target: { value: "nginx.ingress.kubernetes.io/auth-url" } });
    fireEvent.change(screen.getByTestId(`${id}-auth-annotation-0-value`), { target: { value: "https://auth.example.internal/check" } });
    fireEvent.click(screen.getByTestId(`${id}-auth-annotation-add`));
    fireEvent.change(screen.getByTestId(`${id}-auth-annotation-1-name`), { target: { value: "nginx.ingress.kubernetes.io/ssl-redirect" } });
    fireEvent.change(screen.getByTestId(`${id}-auth-annotation-1-value`), { target: { value: "true" } });
    fireEvent.change(screen.getByTestId(`${id}-network-ingress-controller-namespace`), { target: { value: "ingress-nginx" } });
    fireEvent.change(screen.getByTestId(`${id}-network-egress-proxy-url`), { target: { value: "http://proxy.egress.svc:3128" } });
    fireEvent.change(screen.getByTestId(`${id}-network-egress-proxy-cidr`), { target: { value: "10.0.42.0/24" } });
    expect(screen.getByTestId(`${id}-network-dns-pod-label-0-name`)).toHaveValue("k8s-app");
    fireEvent.change(screen.getByTestId(`${id}-quota-memory`), { target: { value: "2Gi" } });
    fireEvent.change(screen.getByTestId(`${id}-quota-pods`), { target: { value: "6" } });
    fireEvent.change(screen.getByTestId(`${id}-container-memory-limit`), { target: { value: "1Gi" } });

    let saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    const defaults = newPocDeployBlocks();
    expect(saved.targets[0].ingress).toEqual({ class: "nginx", tlsIssuer: null });
    expect(saved.targets[0].auth).toEqual({ ingressAnnotations: {
      "nginx.ingress.kubernetes.io/auth-url": "https://auth.example.internal/check",
      "nginx.ingress.kubernetes.io/ssl-redirect": "true",
    } });
    expect(saved.targets[0].network).toEqual({
      ...defaults.network, ingressControllerNamespace: "ingress-nginx",
      egressProxy: { ...defaults.network.egressProxy, url: "http://proxy.egress.svc:3128", cidr: "10.0.42.0/24" },
    });
    expect(saved.targets[0].quota).toEqual({ ...defaults.quota, memory: "2Gi", pods: 6 });
    expect(saved.targets[0].container).toEqual({ ...defaults.container, memoryLimit: "1Gi" });

    // A removed row leaves the map; an emptied optional text sends null.
    fireEvent.click(screen.getByTestId(`${id}-auth-annotation-remove-1`));
    fireEvent.change(screen.getByTestId(`${id}-ingress-class`), { target: { value: "" } });
    saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.targets[0].auth).toEqual({ ingressAnnotations: {
      "nginx.ingress.kubernetes.io/auth-url": "https://auth.example.internal/check",
    } });
    expect(saved.targets[0].ingress).toEqual({ class: null, tlsIssuer: null });
  });

  it("PocsSettingsForm_UntouchedLegacyValue_SendsGitOpsAndEveryTargetsDeployBlocksWithDefaults", () => {
    const seen: SettingValue[] = [];
    const target = { name: "main", sourceRepo: "poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
      domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3, registry: null };
    const legacy = {
      enabled: false, projectAgent: null, projectTracker: null, targets: [target, { ...target, name: "second" }],
      archive: POC_ARCHIVE_DEFAULTS, scan: POC_SCAN_DEFAULTS, blockedRetentionDays: 7, variables: POC_VARIABLES_DEFAULTS,
    } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.gitops).toEqual({ commitAttempts: 3, maxFolderFiles: 500 });
    const defaults = newPocDeployBlocks();
    for (const sent of saved.targets) {
      expect(sent.ingress).toEqual(defaults.ingress);
      expect(sent.auth).toEqual(defaults.auth);
      expect(sent.network).toEqual(defaults.network);
      expect(sent.quota).toEqual(defaults.quota);
      expect(sent.container).toEqual(defaults.container);
    }
  });
});
