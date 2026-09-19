type BaleSettings = {
  botToken: string;
  providerToken: string;
  botUsername: string;
};

type CartItemSnapshot = {
  product_id: string;
  title: string;
  price_irr: number;
  ypoint_amount: number | null;
  vip_server_id: string | null;
  vip_duration: string | null;
  subscription_tier: string | null;
};

async function hasura<T>(
  query: string,
  variables?: Record<string, unknown>,
): Promise<T> {
  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  const adminSecret = process.env.HASURA_GRAPHQL_ADMIN_SECRET;
  if (!apiDomain || !adminSecret) {
    throw createError({
      statusCode: 500,
      statusMessage: "Store backend is not configured",
    });
  }

  const res = await fetch(`https://${apiDomain}/v1/graphql`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "x-hasura-admin-secret": adminSecret,
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
      statusMessage: json.errors?.[0]?.message || "Hasura request failed",
    });
  }
  return json.data as T;
}

async function loadBaleSettings(): Promise<BaleSettings> {
  const data = await hasura<{
    settings: Array<{ name: string; value: string }>;
  }>(
    `query {
      settings(where: {name: {_in: ["bale.bot_token","bale.provider_token","bale.bot_username"]}}) {
        name
        value
      }
    }`,
  );
  const map = Object.fromEntries(
    (data.settings || []).map((r) => [r.name, r.value || ""]),
  );
  return {
    botToken: map["bale.bot_token"] || "",
    providerToken: map["bale.provider_token"] || "",
    botUsername: map["bale.bot_username"] || "",
  };
}

async function baleApi(
  botToken: string,
  method: string,
  body: Record<string, unknown>,
) {
  const res = await fetch(`https://tapi.bale.ai/bot${botToken}/${method}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
  const data = (await res.json().catch(() => ({}))) as {
    ok?: boolean;
    description?: string;
  };
  if (!res.ok || data.ok === false) {
    throw createError({
      statusCode: 502,
      statusMessage: data.description || `Bale ${method} failed`,
    });
  }
  return data;
}

function expandOrderId(compact: string): string | null {
  if (!/^[a-f0-9]{32}$/i.test(compact)) return null;
  const hex = compact.toLowerCase();
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function normalizeCartItems(raw: unknown): CartItemSnapshot[] {
  if (!raw) return [];
  const list = Array.isArray(raw)
    ? raw
    : typeof raw === "string"
      ? (JSON.parse(raw) as unknown)
      : raw;
  if (!Array.isArray(list)) return [];
  return list
    .map((row) => {
      const r = row as Partial<CartItemSnapshot>;
      return {
        product_id: String(r.product_id || ""),
        title: String(r.title || "Item"),
        price_irr: Number(r.price_irr || 0),
        ypoint_amount: r.ypoint_amount == null ? null : Number(r.ypoint_amount),
        vip_server_id: r.vip_server_id ? String(r.vip_server_id) : null,
        vip_duration: r.vip_duration ? String(r.vip_duration) : null,
        subscription_tier: r.subscription_tier
          ? String(r.subscription_tier)
          : null,
      };
    })
    .filter((i) => i.product_id && i.price_irr >= 0);
}

/** Forward paid events to the API for VIP RCON + in-app notifications. */
async function tryFulfillViaApi(payload: string, chargeId: string) {
  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  if (!apiDomain) return;
  try {
    await fetch(`https://${apiDomain}/store/bale-webhook`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        successful_payment: {
          invoice_payload: payload,
          telegram_payment_charge_id: chargeId,
        },
      }),
    });
  } catch {
    // Best-effort when API /store is unreachable.
  }
}

async function creditYpoints(args: {
  steamId: string;
  amount: number;
  refId: string;
}) {
  if (args.amount <= 0) return;
  const existing = await hasura<{
    ypoint_ledger: Array<{ id: string }>;
  }>(
    `query ($steamId: bigint!, $refId: String!) {
      ypoint_ledger(
        where: {
          steam_id: { _eq: $steamId }
          ref_type: { _eq: "store_order" }
          ref_id: { _eq: $refId }
          delta: { _gt: 0 }
        }
        limit: 1
      ) { id }
    }`,
    { steamId: args.steamId, refId: args.refId },
  );
  if (existing.ypoint_ledger?.length) return;

  const updated = await hasura<{
    update_players_by_pk: { ypoint_balance: number } | null;
  }>(
    `mutation ($steamId: bigint!, $amount: Int!) {
      update_players_by_pk(
        pk_columns: { steam_id: $steamId }
        _inc: { ypoint_balance: $amount }
      ) { ypoint_balance }
    }`,
    { steamId: args.steamId, amount: args.amount },
  );
  const balanceAfter = Number(
    updated.update_players_by_pk?.ypoint_balance ?? 0,
  );
  await hasura(
    `mutation (
      $steamId: bigint!
      $delta: Int!
      $balanceAfter: Int!
      $refId: String!
    ) {
      insert_ypoint_ledger_one(object: {
        steam_id: $steamId
        delta: $delta
        balance_after: $balanceAfter
        reason: "store_purchase"
        ref_type: "store_order"
        ref_id: $refId
      }) { id }
    }`,
    {
      steamId: args.steamId,
      delta: args.amount,
      balanceAfter,
      refId: args.refId,
    },
  );
}

export default defineEventHandler(async (event) => {
  const update = await readBody(event);
  const bale = await loadBaleSettings();
  if (!bale.botToken || !bale.providerToken) {
    throw createError({
      statusCode: 503,
      statusMessage: "Bale Pay is not configured",
    });
  }

  if (update?.message?.text) {
    const text = String(update.message.text);
    const chatId = update.message.chat?.id;
    const match = text.match(/^\/start(?:@\w+)?\s+pay_([a-f0-9]{32})$/i);
    if (chatId && match) {
      const orderId = expandOrderId(match[1]);
      if (!orderId) {
        throw createError({ statusCode: 400, statusMessage: "Invalid order" });
      }

      let order: {
        id: string;
        status: string;
        amount_irr: number;
        bale_payload: string;
        cart_items: CartItemSnapshot[] | null;
        product: {
          title: string;
          description: string;
          price_irr: number;
          active: boolean;
        };
      } | null = null;

      try {
        const live = await hasura<{
          store_orders_by_pk: typeof order;
        }>(
          `query ($id: uuid!) {
            store_orders_by_pk(id: $id) {
              id status amount_irr bale_payload cart_items
              product { title description price_irr active }
            }
          }`,
          { id: orderId },
        );
        order = live.store_orders_by_pk;
      } catch {
        // cart_items may not be tracked in Hasura yet — fall back.
        const live = await hasura<{
          store_orders_by_pk: typeof order;
        }>(
          `query ($id: uuid!) {
            store_orders_by_pk(id: $id) {
              id status amount_irr bale_payload
              product { title description price_irr active }
            }
          }`,
          { id: orderId },
        );
        order = live.store_orders_by_pk
          ? { ...live.store_orders_by_pk, cart_items: null }
          : null;
      }
      if (!order) {
        throw createError({ statusCode: 404, statusMessage: "Order not found" });
      }
      if (order.status !== "pending") {
        return { ok: true, status: order.status };
      }

      const cart = normalizeCartItems(order.cart_items);
      let prices: Array<{ label: string; amount: number }>;
      let amountIrr: number;
      let title: string;
      let description: string;

      if (cart.length > 0) {
        prices = cart.map((i) => ({
          label: i.title.slice(0, 32),
          amount: Number(i.price_irr),
        }));
        amountIrr = prices.reduce((s, p) => s + p.amount, 0);
        title =
          cart.length === 1
            ? cart[0].title
            : `YGuard Store (${cart.length} items)`;
        const toman = Math.round(amountIrr / 10);
        description =
          `${cart.map((i) => i.title).join(" · ").slice(0, 180)} · ${toman.toLocaleString("en-US")} تومان`.slice(
            0,
            255,
          );
      } else {
        amountIrr = Number(order.product.price_irr);
        if (amountIrr !== Number(order.amount_irr)) {
          await hasura(
            `mutation ($id: uuid!, $amount: Int!) {
              update_store_orders_by_pk(
                pk_columns: { id: $id }
                _set: { amount_irr: $amount }
              ) { id }
            }`,
            { id: order.id, amount: amountIrr },
          );
        }
        title = order.product.title;
        const toman = Math.round(amountIrr / 10);
        description =
          `${(order.product.description || order.product.title).slice(0, 200)} · ${toman.toLocaleString("en-US")} تومان`.slice(
            0,
            255,
          );
        prices = [
          {
            label: order.product.title.slice(0, 32),
            amount: amountIrr,
          },
        ];
      }

      if (amountIrr !== Number(order.amount_irr) && cart.length > 0) {
        await hasura(
          `mutation ($id: uuid!, $amount: Int!) {
            update_store_orders_by_pk(
              pk_columns: { id: $id }
              _set: { amount_irr: $amount }
            ) { id }
          }`,
          { id: order.id, amount: amountIrr },
        );
      }

      await baleApi(bale.botToken, "sendInvoice", {
        chat_id: chatId,
        title: title.slice(0, 32),
        description,
        payload: order.bale_payload,
        provider_token: bale.providerToken,
        currency: "IRR",
        prices,
      });
      return { ok: true };
    }
  }

  if (update?.pre_checkout_query) {
    await baleApi(bale.botToken, "answerPreCheckoutQuery", {
      pre_checkout_query_id: update.pre_checkout_query.id,
      ok: true,
    });
    return { ok: true };
  }

  const payment =
    update?.message?.successful_payment || update?.successful_payment;
  if (payment) {
    const payload = String(payment.invoice_payload || "");
    const chargeId = String(
      payment.telegram_payment_charge_id ||
        payment.provider_payment_charge_id ||
        "",
    );
    if (payload.startsWith("store:")) {
      let order: {
        id: string;
        buyer_steam_id: string;
        cart_items: CartItemSnapshot[] | null;
        product: {
          ypoint_amount: number | null;
          vip_server_id: string | null;
          vip_duration: string | null;
        };
      } | null = null;

      try {
        const paid = await hasura<{
          update_store_orders: {
            returning: Array<NonNullable<typeof order>>;
          };
        }>(
          `mutation ($payload: String!, $chargeId: String, $paidAt: timestamptz!) {
            update_store_orders(
              where: { bale_payload: { _eq: $payload }, status: { _eq: "pending" } }
              _set: {
                status: "paid"
                paid_at: $paidAt
                bale_payment_charge_id: $chargeId
              }
            ) {
              returning {
                id
                buyer_steam_id
                cart_items
                product { ypoint_amount vip_server_id vip_duration }
              }
            }
          }`,
          {
            payload,
            chargeId: chargeId || null,
            paidAt: new Date().toISOString(),
          },
        );
        order = paid.update_store_orders?.returning?.[0] ?? null;
      } catch {
        const paid = await hasura<{
          update_store_orders: {
            returning: Array<Omit<NonNullable<typeof order>, "cart_items">>;
          };
        }>(
          `mutation ($payload: String!, $chargeId: String, $paidAt: timestamptz!) {
            update_store_orders(
              where: { bale_payload: { _eq: $payload }, status: { _eq: "pending" } }
              _set: {
                status: "paid"
                paid_at: $paidAt
                bale_payment_charge_id: $chargeId
              }
            ) {
              returning {
                id
                buyer_steam_id
                product { ypoint_amount vip_server_id vip_duration }
              }
            }
          }`,
          {
            payload,
            chargeId: chargeId || null,
            paidAt: new Date().toISOString(),
          },
        );
        const row = paid.update_store_orders?.returning?.[0];
        order = row ? { ...row, cart_items: null } : null;
      }

      if (order) {
        const cart = normalizeCartItems(order.cart_items);
        if (cart.length > 0) {
          let idx = 0;
          for (const line of cart) {
            await creditYpoints({
              steamId: order.buyer_steam_id,
              amount: Number(line.ypoint_amount || 0),
              refId: idx === 0 ? order.id : `${order.id}:${idx}`,
            });
            idx += 1;
          }
        } else {
          await creditYpoints({
            steamId: order.buyer_steam_id,
            amount: Number(order.product?.ypoint_amount || 0),
            refId: order.id,
          });
        }
        await tryFulfillViaApi(payload, chargeId);
      }
    }
    return { ok: true };
  }

  return { ok: true, ignored: true };
});
