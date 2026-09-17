/**
 * Bale Pay webhook (set on the bot to https://yguard.ir/api/store/bale-webhook).
 * Forwards to the Nest API so VIP RCON + Ypoint credit live in one place.
 */
export default defineEventHandler(async (event) => {
  const update = await readBody(event);
  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  if (!apiDomain) {
    throw createError({
      statusCode: 500,
      statusMessage: "NUXT_PUBLIC_API_DOMAIN is not configured",
    });
  }

  const headers: Record<string, string> = {
    "content-type": "application/json",
  };
  const secret =
    process.env.BALE_WEBHOOK_SECRET ||
    getHeader(event, "x-bale-webhook-secret") ||
    "";
  if (secret) {
    headers["x-bale-webhook-secret"] = secret;
  }

  const res = await fetch(`https://${apiDomain}/store/bale-webhook`, {
    method: "POST",
    headers,
    body: JSON.stringify(update ?? {}),
  });

  const text = await res.text();
  let data: unknown = { ok: true };
  try {
    data = text ? JSON.parse(text) : { ok: true };
  } catch {
    data = { ok: res.ok, raw: text.slice(0, 200) };
  }

  if (!res.ok) {
    throw createError({
      statusCode: res.status >= 400 ? res.status : 502,
      statusMessage:
        (data as { message?: string; statusMessage?: string })?.message ||
        (data as { statusMessage?: string })?.statusMessage ||
        `API store webhook failed (${res.status})`,
    });
  }

  return data;
});
