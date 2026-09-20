import type { ApolloClient, NormalizedCacheObject } from "@apollo/client/core";
import gql from "graphql-tag";

/** Ranked ladders we consider when picking a player's best Elo. */
export const LANDING_ELO_MODES = [
  "Competitive",
  "Trios",
  "Wingman",
  "Duel",
] as const;

export type LandingEloPlayer = {
  player_steam_id: string;
  player_name: string;
  player_avatar_url: string | null;
  player_custom_avatar_url: string | null;
  player_country: string | null;
  /** Best current Elo across modes. */
  value: number;
  /** Mode that produced `value`. */
  match_type: string;
};

const LANDING_BEST_ELO_QUERY = gql`
  query LandingBestEloByMode(
    $category: String!
    $window_days: Int!
    $match_type: String
    $exclude_tournaments: Boolean!
    $role: String
    $season_id: uuid
    $source: String
    $limit: Int
    $offset: Int
    $order_by: [leaderboard_entries_order_by!]
  ) {
    get_leaderboard(
      args: {
        _category: $category
        _window_days: $window_days
        _match_type: $match_type
        _exclude_tournaments: $exclude_tournaments
        _role: $role
        _season_id: $season_id
        _source: $source
      }
      limit: $limit
      offset: $offset
      order_by: $order_by
    ) {
      player_steam_id
      player_name
      player_avatar_url
      player_custom_avatar_url
      player_country
      value
    }
  }
`;

/**
 * Top players by their highest *current* Elo in any ranked mode.
 *
 * `get_leaderboard` with match_type=null + window_days=0 returns all-time peak
 * across mixed rows (latest-by-time, not max-of-currents). So we fetch each
 * mode with a long window (current Elo, not peak) and keep the max per player.
 */
export async function fetchTopPlayersByBestElo(
  apolloClient: ApolloClient<NormalizedCacheObject>,
  limit: number,
): Promise<LandingEloPlayer[]> {
  // window_days > 0 → leaderboard returns current Elo, not all-time peak.
  const windowDays = 36500;
  // Pull enough per mode so merging still fills `limit` after dedupe.
  const perModeLimit = Math.max(limit * 8, 40);

  const settled = await Promise.all(
    LANDING_ELO_MODES.map(async (match_type) => {
      try {
        const { data } = await apolloClient.query({
          query: LANDING_BEST_ELO_QUERY,
          variables: {
            category: "elo",
            window_days: windowDays,
            match_type,
            exclude_tournaments: false,
            role: null,
            season_id: null,
            source: "overall",
            limit: perModeLimit,
            offset: 0,
            order_by: [{ value: "desc" }],
          },
          fetchPolicy: "network-only",
        });
        return {
          match_type,
          rows: (data?.get_leaderboard ?? []) as Array<Record<string, unknown>>,
        };
      } catch (error) {
        console.error(`landing best-elo fetch failed (${match_type})`, error);
        return { match_type, rows: [] as Array<Record<string, unknown>> };
      }
    }),
  );

  const best = new Map<string, LandingEloPlayer>();

  for (const { match_type, rows } of settled) {
    for (const row of rows) {
      const steamId = String(row.player_steam_id ?? "");
      if (!steamId) continue;
      const elo = Number(row.value) || 0;
      const prev = best.get(steamId);
      if (prev && elo <= prev.value) continue;
      best.set(steamId, {
        player_steam_id: steamId,
        player_name: String(row.player_name || "Player"),
        player_avatar_url: (row.player_avatar_url as string | null) ?? null,
        player_custom_avatar_url:
          (row.player_custom_avatar_url as string | null) ?? null,
        player_country: (row.player_country as string | null) ?? null,
        value: elo,
        match_type,
      });
    }
  }

  return [...best.values()]
    .sort((a, b) => b.value - a.value)
    .slice(0, limit);
}
