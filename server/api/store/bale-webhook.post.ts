type BaleSettings = {
  botToken: string;
  providerToken: string;
  botUsername: string;
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
      const data = await hasura<{
        store_orders_by_pk: {
          id: string;
          status: string;
          amount_irr: number;
          bale_payload: string;
          product: { title: string; description: string };
        } | null;
      }>(
        `query ($id: uuid!) {
          store_orders_by_pk(id: $id) {
            id status amount_irr bale_payload
            product { title description }
          }
        }`,
        { id: orderId },
      );
      const order = data.store_orders_by_pk;
      if (!order) {
        throw createError({ statusCode: 404, statusMessage: "Order not found" });
      }
      if (order.status !== "pending") {
        return { ok: true, status: order.status };
      }
      await baleApi(bale.botToken, "sendInvoice", {
        chat_id: chatId,
        title: order.product.title.slice(0, 32),
        description: (order.product.description || order.product.title).slice(
          0,
          255,
        ),
        payload: order.bale_payload,
        provider_token: bale.providerToken,
        currency: "IRR",
        prices: [
          {
            label: order.product.title.slice(0, 32),
            amount: order.amount_irr,
          },
        ],
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
      await hasura(
        `mutation ($payload: String!, $chargeId: String, $paidAt: timestamptz!) {
          update_store_orders(
            where: { bale_payload: { _eq: $payload }, status: { _eq: "pending" } }
            _set: {
              status: "paid"
              paid_at: $paidAt
              bale_payment_charge_id: $chargeId
            }
          ) { affected_rows }
        }`,
        {
          payload,
          chargeId: chargeId || null,
          paidAt: new Date().toISOString(),
        },
      );
    }
    return { ok: true };
  }

  return { ok: true, ignored: true };
});
