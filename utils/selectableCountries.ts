import { getAllCountries, type Country } from "countries-and-timezones";

/** ISO country codes players must not pick for their profile flag. */
export const BLOCKED_PROFILE_COUNTRY_CODES = new Set(["US", "IL"]);

export function getSelectableCountries(): Record<string, Country> {
  const all = getAllCountries();
  const out: Record<string, Country> = {};
  for (const [id, country] of Object.entries(all)) {
    if (!BLOCKED_PROFILE_COUNTRY_CODES.has(id)) {
      out[id] = country;
    }
  }
  return out;
}

export function isBlockedProfileCountry(code?: string | null): boolean {
  if (!code) return false;
  return BLOCKED_PROFILE_COUNTRY_CODES.has(code.toUpperCase());
}
