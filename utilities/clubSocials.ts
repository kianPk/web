const TELEGRAM_HANDLE = /^[A-Za-z0-9_]{5,32}$/;
const INSTAGRAM_HANDLE = /^[A-Za-z0-9._]{1,30}$/;

export type SocialParse =
  | { ok: true; url: string | null }
  | { ok: false };

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
  const handle = stripHandle(value, /^(t\.me|telegram\.me|telegram\.dog)\//i);
  if (!TELEGRAM_HANDLE.test(handle)) return { ok: false };
  return { ok: true, url: `https://t.me/${handle}` };
}

export function parseInstagramUrl(raw: string): SocialParse {
  const value = raw.trim();
  if (!value) return { ok: true, url: null };
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
