"use client";

import type {
  PocAuthSetting, PocContainerSetting, PocEgressProxySetting, PocIngressSetting, PocNetworkSetting, PocQuotaSetting,
  PocTargetSetting,
} from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { DrawerSection, NumberField, TextField, keep } from "./formFields";
import { PocPairsField } from "./PocPairsField";

// 2026-11-23-c81eb: how a target's PoCs are reached, put behind auth and confined — the Ingress, the auth annotations,
// the NetworkPolicy, the namespace's ResourceQuota and the container's resources, all rendered into every PoC's
// manifests at its next GitOps commit. The server refuses what PocTargetConfig.Problems() names; this form only edits.

export interface PocDeployBlocks {
  ingress: PocIngressSetting;
  auth: PocAuthSetting;
  network: PocNetworkSetting;
  quota: PocQuotaSetting;
  container: PocContainerSetting;
}

/** The server's defaults (PocIngressConfig … PocContainerConfig). A target read without a block is given these and
 *  sends them back, because a save without the block would reset it. */
export function newPocDeployBlocks(): PocDeployBlocks {
  return {
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
  };
}

/** The target with every deploy block present: a missing one is given the defaults. */
export function withPocDeployBlocks(target: PocTargetSetting): PocTargetSetting & PocDeployBlocks {
  const defaults = newPocDeployBlocks();
  return {
    ...target,
    ingress: target.ingress ?? defaults.ingress,
    auth: target.auth ?? defaults.auth,
    network: target.network ?? defaults.network,
    quota: target.quota ?? defaults.quota,
    container: target.container ?? defaults.container,
  };
}

const orNull = (v: string) => (v.trim() ? v.trim() : null);

export const INGRESS_CLASS_PLACEHOLDER = "nginx";
export const TLS_ISSUER_PLACEHOLDER = "letsencrypt";
export const ANNOTATION_NAME_PLACEHOLDER = "nginx.ingress.kubernetes.io/auth-url";
export const ANNOTATION_VALUE_PLACEHOLDER = "https://auth.example.internal/oauth2/auth";
export const EGRESS_PROXY_URL_PLACEHOLDER = "http://egress-proxy.egress.svc.cluster.local:3128";

export function PocDeployFields({ target, testId, onChange }: {
  target: PocTargetSetting; testId: string; onChange: (patch: Partial<PocTargetSetting>) => void;
}) {
  const full = withPocDeployBlocks(target);
  return (
    <>
      <PocIngressFields ingress={full.ingress} testId={`${testId}-ingress`} onChange={(ingress) => onChange({ ingress })} />
      <PocAuthFields auth={full.auth} testId={`${testId}-auth`} onChange={(auth) => onChange({ auth })} />
      <PocNetworkFields network={full.network} testId={`${testId}-network`} onChange={(network) => onChange({ network })} />
      <PocQuotaFields quota={full.quota} testId={`${testId}-quota`} onChange={(quota) => onChange({ quota })} />
      <PocContainerFields container={full.container} testId={`${testId}-container`} onChange={(container) => onChange({ container })} />
    </>
  );
}

function PocIngressFields({ ingress, testId, onChange }: {
  ingress: PocIngressSetting; testId: string; onChange: (ingress: PocIngressSetting) => void;
}) {
  const t = useT();
  return (
    <DrawerSection title={t("config.pocs.ingress.title")} testId={testId}>
      <span className="help">{t("config.pocs.ingress.help")}</span>
      <TextField label={t("config.pocs.ingress.class")} value={ingress.class ?? ""} mono testId={`${testId}-class`}
        placeholder={INGRESS_CLASS_PLACEHOLDER} help={t("config.pocs.ingress.class.help")}
        onChange={(v) => onChange({ ...ingress, class: orNull(v) })} />
      <TextField label={t("config.pocs.ingress.tlsIssuer")} value={ingress.tlsIssuer ?? ""} mono testId={`${testId}-tls-issuer`}
        placeholder={TLS_ISSUER_PLACEHOLDER} help={t("config.pocs.ingress.tlsIssuer.help")}
        onChange={(v) => onChange({ ...ingress, tlsIssuer: orNull(v) })} />
    </DrawerSection>
  );
}

function PocAuthFields({ auth, testId, onChange }: { auth: PocAuthSetting; testId: string; onChange: (auth: PocAuthSetting) => void }) {
  const t = useT();
  return (
    <DrawerSection title={t("config.pocs.auth.title")} testId={testId}>
      <span className="help">{t("config.pocs.auth.help")}</span>
      <PocPairsField label={t("config.pocs.auth.annotations")} help={t("config.pocs.auth.annotations.help")}
        value={auth.ingressAnnotations} testId={`${testId}-annotation`}
        namePlaceholder={ANNOTATION_NAME_PLACEHOLDER} valuePlaceholder={ANNOTATION_VALUE_PLACEHOLDER}
        onChange={(ingressAnnotations) => onChange({ ...auth, ingressAnnotations })} />
    </DrawerSection>
  );
}

function PocNetworkFields({ network, testId, onChange }: {
  network: PocNetworkSetting; testId: string; onChange: (network: PocNetworkSetting) => void;
}) {
  const t = useT();
  const proxy = network.egressProxy;
  const setProxy = (patch: Partial<PocEgressProxySetting>) => onChange({ ...network, egressProxy: { ...proxy, ...patch } });
  const p = `${testId}-egress-proxy`;
  return (
    <DrawerSection title={t("config.pocs.network.title")} testId={testId}>
      <span className="help">{t("config.pocs.network.help")}</span>
      <TextField label={t("config.pocs.network.ingressControllerNamespace")} value={network.ingressControllerNamespace ?? ""} mono
        testId={`${testId}-ingress-controller-namespace`} placeholder="ingress-nginx"
        help={t("config.pocs.network.ingressControllerNamespace.help")}
        onChange={(v) => onChange({ ...network, ingressControllerNamespace: orNull(v) })} />
      <TextField label={t("config.pocs.network.dnsNamespace")} value={network.dnsNamespace} mono testId={`${testId}-dns-namespace`}
        help={t("config.pocs.network.dnsNamespace.help")} onChange={(v) => onChange({ ...network, dnsNamespace: v })} />
      <PocPairsField label={t("config.pocs.network.dnsPodLabels")} help={t("config.pocs.network.dnsPodLabels.help")}
        value={network.dnsPodLabels} testId={`${testId}-dns-pod-label`}
        onChange={(dnsPodLabels) => onChange({ ...network, dnsPodLabels })} />
      <span className="help">{t("config.pocs.network.egressProxy.help")}</span>
      <TextField label={t("config.pocs.network.egressProxy.url")} value={proxy.url ?? ""} mono testId={`${p}-url`}
        placeholder={EGRESS_PROXY_URL_PLACEHOLDER} help={t("config.pocs.network.egressProxy.url.help")}
        onChange={(v) => setProxy({ url: orNull(v) })} />
      <NumberField label={t("config.pocs.network.egressProxy.port")} value={proxy.port} testId={`${p}-port`}
        help={t("config.pocs.network.egressProxy.port.help")} onChange={(v) => setProxy({ port: keep(v, proxy.port) })} />
      <TextField label={t("config.pocs.network.egressProxy.namespace")} value={proxy.namespace ?? ""} mono testId={`${p}-namespace`}
        help={t("config.pocs.network.egressProxy.namespace.help")} onChange={(v) => setProxy({ namespace: orNull(v) })} />
      <PocPairsField label={t("config.pocs.network.egressProxy.podLabels")} help={t("config.pocs.network.egressProxy.podLabels.help")}
        value={proxy.podLabels} testId={`${p}-pod-label`} onChange={(podLabels) => setProxy({ podLabels })} />
      <TextField label={t("config.pocs.network.egressProxy.cidr")} value={proxy.cidr ?? ""} mono testId={`${p}-cidr`}
        placeholder="10.0.42.0/24" help={t("config.pocs.network.egressProxy.cidr.help")}
        onChange={(v) => setProxy({ cidr: orNull(v) })} />
      <TextField label={t("config.pocs.network.egressProxy.noProxy")} value={proxy.noProxy} mono testId={`${p}-no-proxy`}
        help={t("config.pocs.network.egressProxy.noProxy.help")} onChange={(v) => setProxy({ noProxy: v })} />
    </DrawerSection>
  );
}

const QUOTA_TEXT: { key: "cpu" | "memory" | "ephemeralStorage"; id: string; label: TextKey }[] = [
  { key: "cpu", id: "cpu", label: "config.pocs.quota.cpu" },
  { key: "memory", id: "memory", label: "config.pocs.quota.memory" },
  { key: "ephemeralStorage", id: "ephemeral-storage", label: "config.pocs.quota.ephemeralStorage" },
];

function PocQuotaFields({ quota, testId, onChange }: { quota: PocQuotaSetting; testId: string; onChange: (quota: PocQuotaSetting) => void }) {
  const t = useT();
  return (
    <DrawerSection title={t("config.pocs.quota.title")} testId={testId}>
      <span className="help">{t("config.pocs.quota.help")}</span>
      {QUOTA_TEXT.map(({ key, id, label }) => (
        <TextField key={key} label={t(label)} value={quota[key]} mono testId={`${testId}-${id}`}
          onChange={(v) => onChange({ ...quota, [key]: v.trim() })} />
      ))}
      <NumberField label={t("config.pocs.quota.pods")} value={quota.pods} testId={`${testId}-pods`}
        help={t("config.pocs.quota.pods.help")} onChange={(v) => onChange({ ...quota, pods: keep(v, quota.pods) })} />
    </DrawerSection>
  );
}

const CONTAINER_FIELDS: { key: keyof PocContainerSetting; id: string; label: TextKey }[] = [
  { key: "cpuRequest", id: "cpu-request", label: "config.pocs.container.cpuRequest" },
  { key: "cpuLimit", id: "cpu-limit", label: "config.pocs.container.cpuLimit" },
  { key: "memoryRequest", id: "memory-request", label: "config.pocs.container.memoryRequest" },
  { key: "memoryLimit", id: "memory-limit", label: "config.pocs.container.memoryLimit" },
  { key: "ephemeralLimit", id: "ephemeral-limit", label: "config.pocs.container.ephemeralLimit" },
];

function PocContainerFields({ container, testId, onChange }: {
  container: PocContainerSetting; testId: string; onChange: (container: PocContainerSetting) => void;
}) {
  const t = useT();
  return (
    <DrawerSection title={t("config.pocs.container.title")} testId={testId}>
      <span className="help">{t("config.pocs.container.help")}</span>
      {CONTAINER_FIELDS.map(({ key, id, label }) => (
        <TextField key={key} label={t(label)} value={container[key]} mono testId={`${testId}-${id}`}
          onChange={(v) => onChange({ ...container, [key]: v.trim() })} />
      ))}
    </DrawerSection>
  );
}
