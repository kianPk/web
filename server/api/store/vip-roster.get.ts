/** Public VIP roster for public-servers page (active grants only). */
export default defineEventHandler(async () => {
  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  const adminSecret = process.env.HASURA_GRAPHQL_ADMIN_SECRET;
  if (!apiDomain || !adminSecret) return [];

  const res = await fetch(`https://${apiDomain}/v1/graphql`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "x-hasura-admin-secret": adminSecret,
    },
    body: JSON.stringify({
      query: `query {
        store_vip_grants(
          where: {
            _or: [
              { expires_at: { _is_null: true } }
              { expires_at: { _gt: "now()" } }
            ]
          }
          order_by: [{ expires_at: asc_nulls_last }]
        ) {
          steam_id
          server_id
          expires_at
          player { name avatar_url steam_id }
        }
      }`,
    }),
  });
  const json = (await res.json()) as {
    data?: { store_vip_grants?: unknown[] };
    errors?: Array<{ message: string }>;
  };
  if (json.errors?.length) {
    console.error("vip-roster", json.errors[0]?.message);
    return [];
  }
  return json.data?.store_vip_grants ?? [];
});
