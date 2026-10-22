const TELEGRAM_HANDLE = /^[A-Za-z0-9_]{5,32}$/;
const INSTAGRAM_HANDLE = /^[A-Za-z0-9._]{1,30}$/;
const BALE_HANDLE = /^[A-Za-z0-9_]{5,32}$/;
const RUBIKA_HANDLE = /^[A-Za-z0-9_]{3,64}$/;
const SOROUSH_HANDLE = /^[A-Za-z0-9_]{3,32}$/;

export type SocialParse =
  | { ok: true; url: string | null }
  | { ok: false };

function storedHostUrl(raw: string, hosts: RegExp): string | null {
  const value = raw.trim();
  if (!/^https?:\/\//i.test(value)) return null;
  try {
    const url = new URL(value);
    const host = url.hostname.replace(/^www\./i, "");
    if (!hosts.test(host)) return null;
    url.hash = "";
    url.search = "";
    const href = url.toString().replace(/\/$/, "");
    if (href.length < 8 || href.length > 200) return null;
    return href;
  } catch {
    return null;
  }
}

function stripHandle(raw: string, hosts: RegExp): string {
  return raw
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/^(www\.)?/i, "")
    .replace(hosts, "")
    .replace(/^@/, "")
    .split(/[/?#]/)[0]
    .trim();
}

export function parseTelegramUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
  const stored = storedHostUrl(value, /^(t\.me|telegram\.me|telegram\.dog)$/i);
  if (stored) return { ok: true, url: stored };
  const handle = stripHandle(value, /^(t\.me|telegram\.me|telegram\.dog)\//i);
  if (!TELEGRAM_HANDLE.test(handle)) return { ok: false };
  return { ok: true, url: `https://t.me/${handle}` };
}

export function parseInstagramUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
  const stored = storedHostUrl(value, /^(instagram\.com|instagr\.am)$/i);
  if (stored) return { ok: true, url: stored };
  const handle = stripHandle(value, /^(instagram\.com|instagr\.am)\//i);
  if (
    !INSTAGRAM_HANDLE.test(handle) ||
    handle.startsWith(".") ||
    handle.endsWith(".")
  ) {
    return { ok: false };
  }
  return { ok: true, url: `https://www.instagram.com/${handle}` };
}

function pathHandle(raw: string, hosts: RegExp): string {
  const rest = raw
    .trim()
    .replace(/^https?:\/\//i, "")
    .replace(/^(www\.)?/i, "")
    .replace(hosts, "")
    .split(/[?#]/)[0];
  const parts = rest
    .split("/")
    .map((part) => part.replace(/^@/, "").trim())
    .filter(Boolean);
  const skip = new Set(["joingroup", "join", "u", "c", "channel", "post"]);
  if (parts.length >= 2 && skip.has(parts[0].toLowerCase())) return parts[1];
  return parts[0] || "";
}

export function parseBaleUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
  const stored = storedHostUrl(value, /^(ble\.ir|bale\.ai|web\.bale\.ai)$/i);
  if (stored) return { ok: true, url: stored };
  const handle = pathHandle(value, /^(ble\.ir|web\.bale\.ai|bale\.ai)\//i);
  if (!BALE_HANDLE.test(handle)) return { ok: false };
  return { ok: true, url: `https://ble.ir/${handle}` };
}

export function parseRubikaUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
  const stored = storedHostUrl(value, /^(rubika\.ir|web\.rubika\.ir)$/i);
  if (stored) return { ok: true, url: stored };
  const handle = pathHandle(value, /^(rubika\.ir|web\.rubika\.ir)\//i);
  if (!RUBIKA_HANDLE.test(handle)) return { ok: false };
  return { ok: true, url: `https://rubika.ir/${handle}` };
}

export function parseSoroushUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
  const stored = storedHostUrl(value, /^(splus\.ir|sapp\.ir)$/i);
  if (stored) return { ok: true, url: stored };
  const bare = value.replace(/^https?:\/\//i, "").replace(/^(www\.)?/i, "");
  const host = /^sapp\.ir\//i.test(bare) ? "sapp.ir" : "splus.ir";
  const handle = pathHandle(value, /^(splus\.ir|sapp\.ir)\//i);
  if (!SOROUSH_HANDLE.test(handle)) return { ok: false };
  return { ok: true, url: `https://${host}/${handle}` };
}
