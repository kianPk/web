/**
 * Cancel other pending store orders for this Steam ID and notify in-app.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody<{ exceptOrderId?: string; steamId?: string }>(
    event,
  );
  const steamId = String(body?.steamId || "");
  const exceptOrderId = body?.exceptOrderId;
  if (!/^\d{15,20}$/.test(steamId) || !exceptOrderId) {
    throw createError({
      statusCode: 400,
      statusMessage: "steamId and exceptOrderId required",
    });
  }

  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  const adminSecret = process.env.HASURA_GRAPHQL_ADMIN_SECRET;
  if (!apiDomain || !adminSecret) {
    return { cancelled: 0 };
  }

  async function hasura<T>(
    query: string,
    variables?: Record<string, unknown>,
  ): Promise<T> {
    const res = await fetch(`https://${apiDomain}/v1/graphql`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-hasura-admin-secret": adminSecret!,
      },
      body: JSON.stringify({ query, variables }),
    });
    const json = (await res.json()) as {
      data?: T;
      errors?: Array<{ message: string }>;
    };
    if (!res.ok || json.errors?.length) {
      throw createError({
        statusCode: 502,
        statusMessage: json.errors?.[0]?.message || "Hasura failed",
      });
    }
    return json.data as T;
  }

  const cancelled = await hasura<{
    update_store_orders: {
      returning: Array<{ id: string; product: { title: string } }>;
    };
  }>(
    `mutation ($steamId: bigint!, $except: uuid!) {
      update_store_orders(
        where: {
          buyer_steam_id: { _eq: $steamId }
          status: { _eq: "pending" }
          id: { _neq: $except }
        }
        _set: { status: "cancelled" }
      ) {
        returning {
          id
          product { title }
        }
      }
    }`,
    { steamId, except: exceptOrderId },
  );

  const rows = cancelled.update_store_orders?.returning || [];
  for (const row of rows) {
    const title = (row.product?.title || "product")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
    await hasura(
      `mutation ($object: notifications_insert_input!) {
        insert_notifications_one(
          object: $object
          on_conflict: {
            constraint: notifications_pkey
            update_columns: []
          }
        ) { id }
      }`,
      {
        object: {
          type: "StorePurchaseCancelled",
          title: "Purchase cancelled",
          message: `Your pending purchase of <b>${title}</b> was cancelled. <a href="/store">Open Store</a>`,
          role: "user",
          steam_id: steamId,
          entity_id: row.id,
          in_app: true,
        },
      },
    ).catch(() => undefined);
  }

  return { cancelled: rows.length };
});
