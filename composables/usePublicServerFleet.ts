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
export function usePublicServerFleet() {
  // section_mode is newer than the generated client; Zeus passes a field it
  // has no schema entry for through as-is.
  const { result: serversResult, loading } = useSubscription(
    generateSubscription({
      servers: [
        {
          where: {
            _and: [
              { section_mode: { _is_null: false } } as any,
              { connection_string: { _is_null: false } },
              { enabled: { _eq: true } },
              { connected: { _eq: true } },
            ],
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
    const counters = new Map<ServerModeKey, number>();
    const list: FleetServer[] = [];

    for (const row of rows) {
      if (row.server_region?.is_lan) continue;
      const modeKey = sectionModeKey(row.section_mode);
      if (!modeKey) continue;

      const number =
        serverNumber(row.label) ?? (counters.get(modeKey) ?? 0) + 1;
      counters.set(modeKey, number);

      const stats = live.value.get(row.id);
      const players = Number(stats?.players) || 0;
      const capacity =
        Number(row.max_players) ||
        SERVER_MODES.find((m) => m.key === modeKey)!.slots;

      list.push({
        id: row.id,
        number,
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
      });
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
