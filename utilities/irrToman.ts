/** Bale Pay / DB keep amounts in Rials (IRR). UI shows Tomans (1 Toman = 10 Rials). */

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

export function formatTomanAmount(
  irr: number,
  locale: string = "en-US",
): string {
  return irrToToman(irr).toLocaleString(locale);
}
