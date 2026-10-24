import { computed } from "vue";
import { useQuery, useSubscription } from "@vue/apollo-composable";
import { generateQuery, generateSubscription } from "~/graphql/graphqlGen";
import {
  SERVER_MODES,
  liveMapName,
  sectionModeKey,
  serverNumber,
  type ServerModeDefinition,
  type ServerModeKey,
} from "~/utilities/serverModes";

export type FleetServer = {
  id: string;
  /** Position within its mode, the way players refer to a server: "#3". */
  number: number;
  label: string | null;
  type: string;
  game: string;
  region: string | null;
  regionName: string | null;
  connection_link: string | null;
  connection_string: string | null;
  max_players: number | null;
  modeKey: ServerModeKey;
  map: string;
  players: number;
  capacity: number;
  full: boolean;
  /** Public fleet box, club fleet box, or the signed-in user's paid Host rental. */
  kind?: "public" | "club" | "host";
  /** hosted_servers.id when kind === "host". */
  hostedId?: string | null;
  clubId?: string | null;
  clubName?: string | null;
  clubSlug?: string | null;
};

export type FleetMode = ServerModeDefinition & {
  servers: FleetServer[];
  players: number;
};

/**
 * The Servers section's own servers (created by the api per mode, never by
 * hand) that are online, with live map and player counts from the one-minute
 * status ping. All modes are returned even with no servers, so the tabs never
 * shift around.
 */
export function usePublicServerFleet(clubId?: string | null) {
  // Do NOT filter/select hosted_section here: production Hasura may not have
  // tracked that column yet, and a single unknown field empties the whole fleet
  // ("No servers online" on every mode). Public boxes are labeled "Duels #1";
  // paid Host rentals use other labels — we drop those client-side instead.
  // The site Servers page lists public + club boxes together; club rows carry
  // club name/slug for the "open club" affordance. A club page passes clubId
  // to show only that club's boxes.
  const where: Array<Record<string, unknown>> = [
    { section_mode: { _is_null: false } },
    { connection_string: { _is_null: false } },
    { enabled: { _eq: true } },
    { connected: { _eq: true } },
  ];
  if (clubId) where.push({ club_id: { _eq: clubId } });

  const { result: serversResult, loading } = useSubscription(
    generateSubscription({
      servers: [
        {
          where: {
            _and: where as any,
          },
          order_by: [{ label: "asc" as any }],
        },
        {
          id: true,
          label: true,
          type: true,
          game: true,
          region: true,
          connection_link: true,
          connection_string: true,
          max_players: true,
          section_mode: true,
          club_id: true,
          club: {
            id: true,
            name: true,
            slug: true,
            short_name: true,
          },
          server_region: {
            is_lan: true,
            description: true,
          },
        } as any,
      ],
    }),
  );

  const { result: liveResult } = useQuery(
    generateQuery({
      getDedicatedServerInfo: [
        {},
        { id: true, map: true, players: true, lastPing: true },
      ],
    }),
    null,
    () => ({ pollInterval: 20_000 }),
  );

  const live = computed(() => {
    const byId = new Map<string, { map?: string; players?: number }>();
    for (const row of (liveResult.value?.getDedicatedServerInfo ||
      []) as Array<{ id: string; map?: string; players?: number }>) {
      byId.set(row.id, row);
    }
    return byId;
  });

  const servers = computed<FleetServer[]>(() => {
    const rows = ((serversResult.value as any)?.servers || []) as Array<
      Record<string, any>
    >;
    const list: FleetServer[] = [];
    const unlabeled: FleetServer[] = [];

    for (const row of rows) {
      if (row.server_region?.is_lan) continue;
      if (clubId && row.club_id !== clubId) continue;
      // Paid Host rentals share section_mode but use custom / "Host …" labels.
      // Public fleet rows are "Duels #1"; club fleet rows carry club_id.
      const number = serverNumber(row.label);
      const isClub = !!row.club_id;
      if (number == null && !clubId && !isClub) continue;
      const modeKey = sectionModeKey(row.section_mode);
      if (!modeKey) continue;

      const stats = live.value.get(row.id);
      const players = Number(stats?.players) || 0;
      const capacity =
        Number(row.max_players) ||
        SERVER_MODES.find((m) => m.key === modeKey)!.slots;
      const clubName =
        row.club?.name ||
        row.club?.short_name ||
        null;

      const server: FleetServer = {
        id: row.id,
        number: number ?? 0,
        label: row.label,
        type: row.type,
        game: row.game,
        region: row.region,
        regionName: row.server_region?.description || row.region || null,
        connection_link: row.connection_link,
        connection_string: row.connection_string,
        max_players: row.max_players,
        modeKey,
        map: liveMapName(stats?.map),
        players,
        capacity,
        full: players >= capacity,
        kind: isClub ? "club" : "public",
        clubId: row.club_id || row.club?.id || null,
        clubName,
        clubSlug: row.club?.slug || null,
      };
      list.push(server);
      if (number == null) unlabeled.push(server);
    }

    if (unlabeled.length > 0) {
      const used = new Map<string, Set<number>>();
      for (const server of list) {
        if (!server.number) continue;
        const taken = used.get(server.modeKey) ?? new Set<number>();
        taken.add(server.number);
        used.set(server.modeKey, taken);
      }
      for (const server of unlabeled) {
        const taken = used.get(server.modeKey) ?? new Set<number>();
        let next = 1;
        while (taken.has(next)) next++;
        server.number = next;
        taken.add(next);
        used.set(server.modeKey, taken);
      }
    }

    return list.sort((a, b) => a.number - b.number);
  });

  const modes = computed<FleetMode[]>(() =>
    SERVER_MODES.map((mode) => {
      const list = servers.value.filter((s) => s.modeKey === mode.key);
      return {
        ...mode,
        servers: list,
        players: list.reduce((sum, s) => sum + s.players, 0),
      };
    }).sort((a, b) => a.order - b.order),
  );

  const totalPlayers = computed(() =>
    servers.value.reduce((sum, s) => sum + s.players, 0),
  );

  return { loading, servers, modes, totalPlayers };
}

/**
 * The server Quick Play drops a player into: the fullest one that still has
 * room, so a lone player lands where the action already is instead of on an
 * empty box. A map preference narrows the pick when one is set.
 */
export function pickQuickPlayServer(
  servers: FleetServer[],
  map?: string | null,
): FleetServer | null {
  const open = servers.filter(
    (s) => !s.full && s.connection_link && (!map || s.map === map),
  );
  if (open.length === 0) return null;
  return [...open].sort((a, b) => b.players - a.players || a.number - b.number)[0];
}

/**
 * Same AC gate as QuickServerConnect, for the places that connect without
 * rendering that component (Quick Play, table rows).
 */
export async function acAllowsJoin(): Promise<boolean> {
  if (!useAuthStore().me?.steam_id) return true;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    const status = await $fetch<{ required?: boolean; valid?: boolean }>(
      `https://${apiDomain}/plugins/ac/status`,
      { credentials: "include" },
    );
    return !status?.required || !!status?.valid;
  } catch {
    return true;
  }
}
