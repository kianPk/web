/** Bale Pay / DB keep amounts in Rials (IRR). UI shows Tomans (1 Toman = 10 Rials). */

const FA_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
const AR_DIGITS = "٠١٢٣٤٥٦٧٨٩";

/** Normalize Persian/Arabic digits and strip grouping so price inputs save correctly. */
export function parseTomanInput(raw: unknown): number {
  if (typeof raw === "number" && Number.isFinite(raw)) {
    return Math.max(0, Math.floor(raw));
  }
  const normalized = String(raw ?? "")
    .trim()
    .replace(/[۰-۹]/g, (d) => String(FA_DIGITS.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String(AR_DIGITS.indexOf(d)))
    .replace(/[^\d]/g, "");
  if (!normalized) return 0;
  const n = Number(normalized);
  return Number.isFinite(n) ? Math.max(0, Math.floor(n)) : 0;
}

export function irrToToman(irr: number): number {
  const n = Number(irr);
  if (!Number.isFinite(n)) return 0;
  return Math.round(n / 10);
}

export function tomanToIrr(toman: number): number {
  const n = Number(toman);
  if (!Number.isFinite(n)) return 0;
  return Math.max(0, Math.round(n * 10));
}

/** i18n codes often use fa_IR; Intl needs fa-IR. */
export function numberLocaleFor(locale?: string | null): string {
  const raw = String(locale || "").trim().replace(/_/g, "-");
  if (!raw) return "en-US";
  if (raw.toLowerCase().startsWith("fa")) return "fa-IR";
  try {
    // Throws for tags Intl rejects (e.g. leftover underscores / junk).
    Intl.NumberFormat(raw);
    return raw;
  } catch {
    return "en-US";
  }
}

export function formatTomanAmount(
  irr: number,
  locale: string = "en-US",
): string {
  return irrToToman(irr).toLocaleString(numberLocaleFor(locale));
}
