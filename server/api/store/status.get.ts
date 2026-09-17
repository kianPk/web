export default defineEventHandler(async () => {
  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  const adminSecret = process.env.HASURA_GRAPHQL_ADMIN_SECRET;
  const webDomain = process.env.NUXT_PUBLIC_WEB_DOMAIN || "yguard.ir";

  if (!apiDomain || !adminSecret) {
    return {
      configured: false,
      botUsername: null,
      webhookHint: `https://${webDomain}/api/store/bale-webhook`,
    };
  }

  const res = await fetch(`https://${apiDomain}/v1/graphql`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "x-hasura-admin-secret": adminSecret,
    },
    body: JSON.stringify({
      query: `query {
        settings(where: {name: {_in: ["bale.bot_token","bale.provider_token","bale.bot_username"]}}) {
          name
          value
        }
      }`,
    }),
  });
  const json = (await res.json()) as {
    data?: { settings: Array<{ name: string; value: string }> };
  };
  const map = Object.fromEntries(
    (json.data?.settings || []).map((r) => [r.name, r.value || ""]),
  );
  return {
    configured: Boolean(map["bale.bot_token"] && map["bale.provider_token"]),
    botUsername: map["bale.bot_username"] || null,
    webhookHint: `https://${webDomain}/api/store/bale-webhook`,
  };
});
