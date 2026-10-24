export type LandingLevelPlayer = {
  player_steam_id: string;
  player_name: string;
  player_avatar_url: string | null;
  player_custom_avatar_url: string | null;
  player_country: string | null;
  level: number;
  xp: number;
};

export async function fetchTopPlayersBySiteLevel(
  limit: number,
): Promise<LandingLevelPlayer[]> {
  const apiDomain = useRuntimeConfig().public.apiDomain as string;
  const data = await $fetch<{
    players: Array<{
      steam_id: string;
      name: string;
      avatar_url: string | null;
      custom_avatar_url: string | null;
      country: string | null;
      level: number;
      xp: number;
    }>;
  }>(`https://${apiDomain}/hosted-servers/site-xp/top?limit=${limit}`);

  return (data?.players ?? []).map((row) => ({
    player_steam_id: row.steam_id,
    player_name: row.name || "Player",
    player_avatar_url: row.avatar_url,
    player_custom_avatar_url: row.custom_avatar_url,
    player_country: row.country,
    level: row.level,
    xp: row.xp,
  }));
}
