import { tryUseNuxtApp } from "#app/nuxt";

export type BrowserClock = {
  locale: string;
  hourCycle: string | undefined;
};

let resolvedCache:
  | { ui: string; browser: string | null; value: string | undefined }
  | undefined;

function uiLocale(): string | undefined {
  const locale = (tryUseNuxtApp()?.$i18n as { locale?: unknown } | undefined)
    ?.locale;
  const value =
    typeof locale === "string"
      ? locale
      : (locale as { value?: unknown } | undefined)?.value;

  return typeof value === "string" && value.length > 0 ? value : undefined;
}

// The user's content language, not Intl's default: Firefox resolves the
// default to its own UI language, whatever the user asked pages to use.
export function browserLanguage(): string | null {
  if (typeof navigator === "undefined") {
    return null;
  }
  return navigator.languages?.[0] || navigator.language || null;
}

export function browserClock(locale: string | null): BrowserClock | null {
  if (!locale) {
    return null;
  }
  try {
    return {
      locale,
      hourCycle: new Intl.DateTimeFormat(locale, {
        hour: "numeric",
      }).resolvedOptions().hourCycle,
    };
  } catch {
    return null;
  }
}

function sameWritingSystem(a: Intl.Locale, b: Intl.Locale) {
  const left = a.maximize();
  const right = b.maximize();
  return left.language === right.language && left.script === right.script;
}

// Words and order follow the UI language, the 12/24-hour clock follows the
// browser; a browser that speaks the UI language lends its whole region
// (en-GB gets "17 Oct" as well as 21:30).
export function resolveDateLocale(
  ui: string | undefined,
  browser: BrowserClock | null,
): string | undefined {
  if (!ui) {
    return undefined;
  }
  if (!browser?.locale) {
    return ui;
  }
  try {
    const browserLocale = new Intl.Locale(browser.locale);
    const base = sameWritingSystem(new Intl.Locale(ui), browserLocale)
      ? browserLocale
      : new Intl.Locale(ui);
    if (!browser.hourCycle) {
      return base.toString();
    }
    return new Intl.Locale(base, {
      hourCycle: browser.hourCycle as Intl.LocaleHourCycleKey,
    }).toString();
  } catch {
    return ui;
  }
}

export function dateLocale(): string | undefined {
  const ui = uiLocale();
  if (!ui) {
    return undefined;
  }
  const browser = browserLanguage();
  if (resolvedCache?.ui !== ui || resolvedCache.browser !== browser) {
    resolvedCache = {
      ui,
      browser,
      value: resolveDateLocale(ui, browserClock(browser)),
    };
  }
  return resolvedCache.value;
}

// Fork helper: club/shop copy formats dates on the site calendar. `fa_IR` is
// not a valid BCP-47 tag, so underscores become hyphens and Persian is forced
// when the UI is Farsi. Always rebuild a clean locale tag — appending
// `-u-ca-persian` onto an existing `-u-hc-…` tag from dateLocale() throws in
// Intl and blanked the club Feed/News panels.
export function formatSiteDate(
  value: Date | string | number | null | undefined,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (value == null || value === "") return "";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);

  const raw = dateLocale() || uiLocale() || "fa-IR";
  const lang = raw.replace(/_/g, "-").split("-")[0]?.toLowerCase();
  const locale = lang === "fa" ? "fa-IR-u-ca-persian" : "en-US-u-ca-persian";

  try {
    return date.toLocaleString(locale, {
      calendar: "persian",
      ...options,
    });
  } catch {
    try {
      return date.toLocaleString("fa-IR", options);
    } catch {
      return date.toISOString();
    }
  }
}
