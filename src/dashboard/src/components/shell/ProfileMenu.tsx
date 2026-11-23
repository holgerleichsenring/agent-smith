"use client";

import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";
import { HubConnectionState } from "@microsoft/signalr";
import { Activity, LogIn, LogOut, Server, Settings, ShieldCheck, UserRound, type LucideIcon } from "lucide-react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useSessionEnd } from "@/hooks/useSessionEnd";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { useLanguage, useT } from "@/hooks/useT";
import { chooseLanguage } from "@/lib/i18n/language";
import { LANGUAGES, type Language, type TextKey } from "@/lib/i18n/translate";
import type { SessionEndReason } from "@/lib/auth/AccessTokenStore";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { useAdminHoldCount } from "@/hooks/useAdminHoldCount";
import { signIn, signOut } from "@/lib/auth/session";
import { cn } from "@/lib/utils";

// 2026-11-16-0440c: the sidebar's foot — who is signed in and the installation they are on, and
// behind it everything that is a setting rather than work: configuration, access, the
// installation, the running system and the caller's identity. It replaces the header's gear and
// account. As before (2026-08-25-4530), an installation with no authority shows no account and
// mounts no identity hook: the foot then names the installation alone.

const SETTINGS: ReadonlyArray<{ id: string; label: TextKey; href: string; icon: LucideIcon }> = [
  { id: "settings", label: "shell.profile.settings", href: "/config", icon: Settings },
  { id: "access", label: "shell.profile.access", href: "/config/access", icon: ShieldCheck },
  { id: "installation", label: "shell.profile.installation", href: "/config/installation", icon: Server },
  { id: "system", label: "shell.profile.system", href: "/system", icon: Activity },
];

// 2026-08-28-0f46: a session that ENDED reads differently from one that never began.
const WHY_IT_ENDED: Record<SessionEndReason, TextKey> = {
  expired: "shell.profile.sessionExpired",
  "renewal-refused": "shell.profile.sessionEnded",
};

// 2026-11-22-5485a: each language is named in itself and flagged, so a reader who cannot read the page still finds
// their own. Not dictionary entries: the name is the same whichever language is in force.
const LANGUAGE_CHOICES: Record<Language, { flag: string; name: string }> = {
  en: { flag: "\u{1F1EC}\u{1F1E7}", name: "English" },
  de: { flag: "\u{1F1E9}\u{1F1EA}", name: "Deutsch" },
};

export function ProfileMenu({ collapsed }: { collapsed: boolean }) {
  const t = useT();
  const language = useLanguage();
  const { auth } = useRuntimeSettings();
  const { connectionState } = useJobsHub();
  const connected = connectionState === HubConnectionState.Connected;
  // The document says which language it is in, so a screen reader speaks it in that voice.
  useEffect(() => {
    document.documentElement.lang = language;
  }, [language]);
  if (!auth.authority) {
    return <Foot collapsed={collapsed} name={t("shell.profile.thisInstallation")} connected={connected} />;
  }
  return <Account collapsed={collapsed} connected={connected} />;
}

function Account({ collapsed, connected }: { collapsed: boolean; connected: boolean }) {
  const t = useT();
  const token = useAccessToken();
  const ended = useSessionEnd();
  const { identity } = useCallerIdentity(token !== null);

  if (token === null) {
    return (
      <Foot collapsed={collapsed} name={t("shell.profile.notSignedIn")} connected={connected} note={ended ? t(WHY_IT_ENDED[ended]) : undefined}>
        <MenuButton icon={LogIn} label={t("shell.profile.signIn")} testId="profile-sign-in" onClick={() => void signIn()} />
      </Foot>
    );
  }
  return (
    <Foot collapsed={collapsed} name={identity?.subject ?? t("shell.profile.signedIn")} role={identity?.roles[0]} connected={connected}>
      <MenuLink icon={UserRound} label={t("shell.profile.identity")} href="/identity" testId="profile-identity" />
      <MenuButton icon={LogOut} label={t("shell.profile.signOut")} testId="profile-sign-out" onClick={() => void signOut()} />
    </Foot>
  );
}

interface FootProps {
  collapsed: boolean;
  name: string;
  role?: string;
  connected: boolean;
  note?: string;
  children?: ReactNode;
}

// 2026-11-18: the menu is portalled to <body> and placed fixed against the profile button. Inside the
// sidebar it was clipped by the sidebar's overflow and painted under a page's own sticky section bar
// (a ticket, Work it out) whenever the sidebar was collapsed to icons; out here nothing clips it.
const MENU_GAP = 6;

function anchorOf(button: HTMLElement | null): CSSProperties | null {
  if (!button) return null;
  const rect = button.getBoundingClientRect();
  return { left: rect.left, bottom: window.innerHeight - rect.top + MENU_GAP };
}

function Foot({ collapsed, name, role, connected, note, children }: FootProps) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [anchor, setAnchor] = useState<CSSProperties | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    if (!open) return;
    const place = () => setAnchor(anchorOf(button.current));
    place();
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
    };
  }, [open, collapsed]);
  useEffect(() => {
    if (!open) return;
    const close = (event: Event) => {
      if (event instanceof KeyboardEvent) {
        if (event.key !== "Escape") return;
        setOpen(false);
        button.current?.focus();
        return;
      }
      const target = event.target as Node;
      if (!root.current?.contains(target) && !menu.current?.contains(target)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", close);
    };
  }, [open]);
  const status = note ?? [t(connected ? "shell.profile.connected" : "shell.profile.offline"), role].filter(Boolean).join(" · ");
  // 2026-11-23-31dci: an admin sees how many admin holds wait under System → Needs attention, on System and on the foot.
  const adminHolds = useAdminHoldCount();

  return (
    <div className="sb-foot" ref={root} data-testid="profile">
      {open && anchor && createPortal(
        <div className="sb-menu" role="menu" ref={menu} style={anchor} data-testid="profile-menu" onClick={() => setOpen(false)}>
          {SETTINGS.map((item) => (
            <MenuLink key={item.href} icon={item.icon} label={t(item.label)} href={item.href} testId={`profile-${item.id}`}
              count={item.id === "system" ? adminHolds : undefined} />
          ))}
          <div className="sb-menu-rule" role="separator" />
          <LanguageSwitch />
          {children && <div className="sb-menu-rule" role="separator" />}
          {children}
        </div>,
        document.body,
      )}
      <button
        ref={button}
        type="button"
        className="sb-who"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={collapsed ? t("shell.profile.menuLabel", { name }) : undefined}
        title={name}
        onClick={() => setOpen(!open)}
        data-testid="profile-button"
      >
        <span className="sb-avatar" aria-hidden="true">{name.slice(0, 1).toUpperCase()}</span>
        {(adminHolds ?? 0) > 0 && <span className="sb-dot" data-testid="profile-attention-dot" aria-hidden="true" />}
        {!collapsed && (
          <span className="sb-who-text">
            <span className="sb-who-name" data-testid="profile-name">{name}</span>
            <span className="sb-who-sub" data-testid="profile-status">
              <span className={cn("sb-live", connected && "on")} data-testid="profile-connection" aria-hidden="true" />
              {status}
            </span>
          </span>
        )}
      </button>
    </div>
  );
}

/** 2026-11-22-5485a: the language this browser shows the dashboard in, one flag per language; the one in force is checked. */
function LanguageSwitch() {
  const t = useT();
  const language = useLanguage();
  return (
    <div className="sb-menu-lang" role="group" aria-label={t("shell.profile.language")} data-testid="profile-language">
      {LANGUAGES.map((choice) => (
        <button
          key={choice}
          type="button"
          role="menuitemradio"
          aria-checked={choice === language}
          lang={choice}
          title={LANGUAGE_CHOICES[choice].name}
          data-testid={`profile-language-${choice}`}
          onClick={() => chooseLanguage(choice)}
        >
          <span aria-hidden="true">{LANGUAGE_CHOICES[choice].flag}</span>
          {LANGUAGE_CHOICES[choice].name}
        </button>
      ))}
    </div>
  );
}

function MenuLink({ icon: Icon, label, href, testId, count }: {
  icon: LucideIcon; label: string; href: string; testId: string; count?: number;
}) {
  return (
    <Link href={href} role="menuitem" data-testid={testId}>
      <Icon size={15} strokeWidth={1.75} aria-hidden="true" />
      {label}
      {count !== undefined && <span className={cn("sb-count", count > 0 && "hot")} data-testid={`${testId}-count`}>{count}</span>}
    </Link>
  );
}

function MenuButton({ icon: Icon, label, onClick, testId }: { icon: LucideIcon; label: string; onClick: () => void; testId: string }) {
  return (
    <button type="button" role="menuitem" onClick={onClick} data-testid={testId}>
      <Icon size={15} strokeWidth={1.75} aria-hidden="true" />
      {label}
    </button>
  );
}
