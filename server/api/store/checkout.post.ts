/**
 * Authenticated checkout: creates the order on the API with the live DB price
 * (never trusts a client-supplied amount_irr).
 */
export default defineEventHandler(async (event) => {
  const body = await readBody<{ productId?: string }>(event);
  if (!body?.productId) {
    throw createError({ statusCode: 400, statusMessage: "productId required" });
  }

  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  if (!apiDomain) {
    throw createError({
      statusCode: 500,
      statusMessage: "NUXT_PUBLIC_API_DOMAIN is not configured",
    });
  }

  const cookie = getHeader(event, "cookie") || "";
  const res = await fetch(`https://${apiDomain}/store/checkout`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      cookie,
    },
    body: JSON.stringify({ productId: body.productId }),
  });

  const text = await res.text();
  let data: Record<string, unknown> = {};
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    data = { message: text.slice(0, 200) };
  }

  if (!res.ok) {
    throw createError({
      statusCode: res.status >= 400 ? res.status : 502,
      statusMessage:
        (data.message as string) ||
        (data.statusMessage as string) ||
        `Checkout failed (${res.status})`,
    });
  }

  return data;
});
