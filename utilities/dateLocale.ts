import { tryUseNuxtApp } from "#app";

// Site dates are Jalali (Shamsi). `fa_IR` is not a valid BCP-47 tag, so Intl
// used to fall back to the browser calendar (Gregorian). Underscores become
// hyphens, then the Persian calendar is forced on every locale.
export function dateLocale(): string {
  const locale = (tryUseNuxtApp()?.$i18n as { locale?: unknown } | undefined)
    ?.locale;
  const value =
    typeof locale === "string"
      ? locale
      : (locale as { value?: unknown } | undefined)?.value;
  const tag =
    typeof value === "string" && value.length > 0
      ? value.replace(/_/g, "-")
      : "fa-IR";
  const lang = tag.split("-")[0]?.toLowerCase();
  if (lang === "fa") return "fa-IR-u-ca-persian";
  return `${tag}-u-ca-persian`;
}

export function formatSiteDate(
  value: Date | string | number | null | undefined,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (value == null || value === "") return "";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleString(dateLocale(), {
    calendar: "persian",
    ...options,
  });
}
