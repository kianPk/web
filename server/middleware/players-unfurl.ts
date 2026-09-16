// Link-unfurl shim for player profiles. SPA pages never reach crawlers with
// client-set meta; bots get a YGuard-branded OG card here instead.

const PLAYER_QUERY = `query PlayerForUnfurl($steamId: bigint!) {
  players_by_pk(steam_id: $steamId) {
    steam_id
    name
    avatar_url
    role
  }
}`;

export default defineEventHandler(async (event) => {
  if (event.method !== "GET") {
    return;
  }

  const url = getRequestURL(event);
  if (url.searchParams.has("ufl")) {
    return;
  }

  const match = url.pathname.match(/^\/players\/(\d+)\/?$/);
  if (!match) {
    return;
  }

  const steamId = match[1];
  const ua = getRequestHeader(event, "user-agent") || "";
  if (!BOT_UA.test(ua)) {
    return;
  }

  const apiDomain = process.env.NUXT_PUBLIC_API_DOMAIN;
  const adminSecret = process.env.HASURA_GRAPHQL_ADMIN_SECRET;
  if (!apiDomain || !adminSecret) {
    return;
  }

  let player: {
    steam_id: string;
    name: string | null;
    avatar_url: string | null;
    role: string | null;
  } | null = null;

  try {
    const res = await $fetch<{ data?: any }>(`https://${apiDomain}/v1/graphql`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-hasura-admin-secret": adminSecret,
      },
      body: {
        query: PLAYER_QUERY,
        variables: { steamId },
      },
    });
    player = res?.data?.players_by_pk ?? null;
  } catch (err) {
    console.error("[players-unfurl] fetch failed:", err);
  }

  const origin = `${getRequestProtocol(event)}://${getRequestHost(event)}`;
  const pageUrl = `${origin}/players/${steamId}`;
  const humanUrl = `${pageUrl}?ufl=1`;
  const name = player?.name?.trim() || `Player ${steamId}`;
  const role = player?.role ? String(player.role).replace(/_/g, " ") : null;

  setResponseHeader(event, "Cache-Control", "public, max-age=60, must-revalidate");
  setResponseHeader(event, "Content-Type", "text/html; charset=utf-8");

  return renderUnfurl({
    title: `${name} · YGuard`,
    description: role
      ? `${name} on YGuard — ${role}`
      : `${name} on YGuard — Counter-Strike player profile`,
    pageUrl,
    humanUrl,
    image: player?.avatar_url || `${origin}/_ipx/_/favicon/512.png`,
    imageAlt: name,
    type: "profile",
  });
});
