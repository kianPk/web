<script setup lang="ts">
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Settings2 } from "lucide-vue-next";
import { AnimatedCard } from "@/components/ui/animated-card";
import AnimatedFilters from "~/components/common/AnimatedFilters.vue";
import cleanMapName from "~/utilities/cleanMapName";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import QuickServerConnect from "~/components/match/QuickServerConnect.vue";
import { generateQuery, generateSubscription } from "~/graphql/graphqlGen";
import { $ } from "~/generated/zeus";
import { e_server_types_enum } from "~/generated/zeus";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import {
  tacticalCtaButtonClasses,
  tacticalHeaderActionClasses,
} from "~/utilities/tacticalClasses";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import { computed } from "vue";
import { useAuthStore } from "~/stores/AuthStore";
import { e_player_roles_enum } from "~/generated/zeus";

const canManage = computed(() =>
  useAuthStore().isRoleAbove(e_player_roles_enum.moderator),
);
</script>

<template>
  <PageTransition :delay="0">
    <TacticalPageHeader inline-actions>
      <template #title>{{ $t("pages.public_servers.title") }}</template>

      <template v-if="canManage && servers && servers.length" #actions>
        <NuxtLink
          to="/dedicated-servers/create"
          :class="[
            tacticalCtaButtonClasses,
            tacticalHeaderActionClasses,
            'max-md:aspect-square max-md:!px-0',
          ]"
          :title="$t('pages.public_servers.setup_public_server')"
        >
          <Settings2 class="h-4 w-4" />
          <span class="hidden md:inline">{{
            $t("pages.public_servers.setup_public_server")
          }}</span>
        </NuxtLink>
      </template>
    </TacticalPageHeader>
  </PageTransition>

  <PageTransition :delay="100">
    <div class="mt-6">
      <Transition
        mode="out-in"
        enter-active-class="transition-opacity duration-200 ease-out"
        leave-active-class="transition-opacity duration-200 ease-out"
        enter-from-class="opacity-0"
        leave-to-class="opacity-0"
      >
        <!-- Loading -->
        <div
          v-if="loading"
          key="loading"
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
        >
          <div
            v-for="i in 3"
            :key="i"
            class="rounded-xl border overflow-hidden"
          >
            <Skeleton class="h-36 w-full" />
            <div class="p-4 space-y-2">
              <Skeleton class="h-4 w-3/4" />
              <Skeleton class="h-2 w-full" />
              <Skeleton class="h-9 w-full" />
            </div>
          </div>
        </div>

        <!-- Empty -->
        <Empty
          v-else-if="!servers || (servers as any[]).length === 0"
          key="empty"
          class="min-h-[200px]"
        >
          <EmptyTitle>{{
            $t("pages.public_servers.no_servers_title")
          }}</EmptyTitle>
          <EmptyDescription>{{
            canManage
              ? $t("pages.public_servers.no_public_servers_admin")
              : $t("pages.public_servers.no_public_servers")
          }}</EmptyDescription>
          <Button v-if="canManage" as-child>
            <NuxtLink to="/dedicated-servers/create">
              <Settings2 class="h-4 w-4" />
              {{ $t("pages.public_servers.setup_public_server") }}
            </NuxtLink>
          </Button>
        </Empty>

        <!-- Server cards -->
        <div v-else key="servers" class="space-y-8">
          <!-- Only shown once modes are actually in use: on a deployment that
               runs none, a filter with a single option is noise. -->
          <AnimatedFilters
            v-if="modeFilters.length > 2"
            v-model="modeFilter"
            square
            :options="modeFilters"
          />

          <div v-for="(gameServers, game) in serversByGame" :key="game">
            <div class="flex items-center gap-3 mb-5">
              <div class="w-0.5 h-4 rounded-full bg-primary shrink-0" />
              <span
                class="text-xs font-bold uppercase tracking-[0.15em] text-muted-foreground whitespace-nowrap"
              >
                {{ gameLabel(game) }}
              </span>
              <div class="flex-1 h-px bg-border" />
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <AnimatedCard
                v-for="server of matchingMode(flattenGame(gameServers))"
                :key="server.id"
                variant="elevated"
                class="overflow-hidden group cursor-pointer p-0"
              >
                <!-- Zone A: Map Hero -->
                <div class="relative h-36 rounded-t-xl overflow-hidden">
                  <img
                    :src="`/img/maps/screenshots/${mapName(server.id)}.webp`"
                    :alt="mapName(server.id)"
                    class="h-full w-full object-cover group-hover:scale-105 transition-transform duration-700"
                    @error="onImgError"
                  />
                  <div
                    class="absolute inset-0 bg-gradient-to-t from-black/80 via-black/30 to-transparent"
                  />
                  <!-- Top-left: map name -->
                  <div class="absolute top-0 left-0 right-0 px-2 pt-2">
                    <span
                      class="text-[11px] font-bold text-white/90 uppercase tracking-widest drop-shadow-lg"
                    >
                      {{ cleanMapName(mapName(server.id)) }}
                    </span>
                  </div>
                  <!-- Patch centered -->
                  <div
                    class="absolute inset-0 flex items-center justify-center"
                  >
                    <img
                      v-if="mapPatch(server.id)"
                      :src="mapPatch(server.id)"
                      class="w-1/4 max-w-[72px] h-auto max-h-[60%] object-contain drop-shadow-2xl opacity-80"
                    />
                  </div>
                  <!-- Top-right: server type -->
                  <div class="absolute top-2 right-2">
                    <Badge variant="secondary" class="text-xs">{{
                      server.type
                    }}</Badge>
                  </div>
                  <!-- Bottom-right: region -->
                  <div class="absolute bottom-2 right-2">
                    <Badge
                      variant="outline"
                      class="border-white/20 text-white/70 text-xs"
                      >{{ server.region }}</Badge
                    >
                  </div>
                </div>

                <!-- Zone B: Card Body -->
                <div class="px-4 pt-3 pb-2">
                  <div class="mb-2 flex items-center gap-2">
                    <p class="min-w-0 flex-1 truncate font-semibold">
                      {{ server.label }}
                    </p>
                    <!-- What the server is actually running. A name alone does
                         not tell anyone whether this is retakes or vanilla. -->
                    <span
                      v-if="server.game_mode"
                      class="shrink-0 rounded border border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.08)] px-1.5 py-0.5 font-mono text-[0.6rem] font-semibold uppercase tracking-[0.1em] text-[hsl(var(--tac-amber))]"
                    >
                      {{ server.game_mode.name }}
                    </span>
                  </div>
                  <div class="flex items-center justify-between text-sm mb-1.5">
                    <span class="text-muted-foreground">{{
                      $t("pages.public_servers.players")
                    }}</span>
                    <span
                      :class="capacityClass(server)"
                      class="font-mono font-medium"
                    >
                      {{ getDedicatedServerPlayers(server.id) }} /
                      {{ server.max_players }}
                    </span>
                  </div>
                  <div
                    class="relative h-1.5 w-full overflow-hidden rounded-full bg-primary/20"
                  >
                    <div
                      class="h-full rounded-full transition-all"
                      :class="capacityBarClass(server)"
                      :style="`width: ${capacityPercent(server)}%`"
                    />
                  </div>

                  <div class="mt-3 border-t border-border/60 pt-2">
                    <Button
                      as-child
                      variant="outline"
                      size="sm"
                      class="h-8 w-full justify-between gap-2 px-2.5 font-normal"
                    >
                      <NuxtLink
                        :to="`/public-servers/${server.id}`"
                        class="flex w-full items-center justify-between gap-2"
                      >
                        <span
                          class="truncate font-mono text-[0.65rem] font-semibold uppercase tracking-[0.12em] text-muted-foreground"
                        >
                          {{ $t("pages.public_servers.details.button") }}
                        </span>
                        <Settings2 class="h-3.5 w-3.5 shrink-0 opacity-60" />
                      </NuxtLink>
                    </Button>
                  </div>
                </div>

                <!-- Zone C: CTA Footer -->
                <div class="px-4 pb-4 pt-3">
                  <div class="flex items-center gap-2">
                    <div
                      class="flex-1 [&>div]:w-full [&_a]:flex-1 [&_a_button]:w-full"
                    >
                      <QuickServerConnect :server="server" highlight />
                    </div>
                    <Button
                      v-if="canManage"
                      as-child
                      variant="outline"
                      size="icon"
                      :title="$t('pages.public_servers.manage')"
                    >
                      <NuxtLink
                        :to="`/dedicated-servers/${server.id}`"
                        :aria-label="$t('pages.public_servers.manage')"
                      >
                        <Settings2 class="h-4 w-4" />
                      </NuxtLink>
                    </Button>
                  </div>
                </div>
              </AnimatedCard>
            </div>
          </div>
        </div>
      </Transition>
    </div>
  </PageTransition>

  <!-- Public ranks ladder -->
  <PageTransition :delay="150">
    <div v-if="ranksLeaderboard.length" class="mt-10">
      <div class="flex items-center gap-3 mb-5">
        <div class="w-0.5 h-4 rounded-full bg-primary shrink-0" />
        <span
          class="text-xs font-bold uppercase tracking-[0.15em] text-muted-foreground whitespace-nowrap"
        >
          {{ $t("pages.public_servers.ranks_title") }}
        </span>
        <div class="flex-1 h-px bg-border" />
      </div>
      <div class="rounded-xl border border-border divide-y divide-border overflow-hidden">
        <div
          v-for="(row, index) in ranksLeaderboard"
          :key="row.steam_id"
          class="flex items-center gap-3 px-4 py-2.5 text-sm"
        >
          <span class="w-6 font-mono text-muted-foreground tabular-nums"
            >#{{ index + 1 }}</span
          >
          <img
            v-if="csRankIcon(12, row.skill_group)"
            :src="csRankIcon(12, row.skill_group)!"
            :alt="row.rank_name"
            class="h-6 w-6 shrink-0"
          />
          <span class="min-w-0 flex-1 truncate font-medium">{{
            row.name || row.steam_id
          }}</span>
          <span class="hidden sm:inline text-muted-foreground truncate">{{
            row.rank_name
          }}</span>
          <span class="font-mono tabular-nums text-[hsl(var(--tac-amber))]">{{
            row.points
          }}</span>
        </div>
      </div>
    </div>
  </PageTransition>

  <!-- LAN Servers -->
  <PageTransition :delay="200">
    <div v-if="!loading && lanServers && lanServers.length > 0" class="mt-8">
      <div class="flex items-center gap-3 mb-5">
        <div class="w-0.5 h-4 rounded-full bg-muted-foreground/40 shrink-0" />
        <span
          class="text-xs font-bold uppercase tracking-[0.15em] text-muted-foreground/60 whitespace-nowrap"
        >
          {{ $t("pages.public_servers.lan_servers_title") }}
        </span>
        <div class="flex-1 h-px bg-border" />
      </div>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <AnimatedCard
          v-for="server of lanServers"
          :key="server.id"
          variant="elevated"
          class="overflow-hidden group cursor-pointer p-0"
        >
          <!-- Zone A: Map Hero -->
          <div class="relative h-36 rounded-t-xl overflow-hidden">
            <img
              :src="`/img/maps/screenshots/${mapName(server.id)}.webp`"
              :alt="mapName(server.id)"
              class="h-full w-full object-cover group-hover:scale-105 transition-transform duration-700"
              @error="onImgError"
            />
            <div
              class="absolute inset-0 bg-gradient-to-t from-black/80 via-black/30 to-transparent"
            />
            <!-- Top-left: map name -->
            <div class="absolute top-0 left-0 right-0 px-2 pt-2">
              <span
                class="text-[11px] font-bold text-white/90 uppercase tracking-widest drop-shadow-lg"
              >
                {{ cleanMapName(mapName(server.id)) }}
              </span>
            </div>
            <!-- Top-right: server type -->
            <div class="absolute top-2 right-2">
              <Badge variant="secondary" class="text-xs">{{
                server.type
              }}</Badge>
            </div>
            <!-- Bottom-right: region -->
            <div class="absolute bottom-2 right-2">
              <Badge
                variant="outline"
                class="border-white/20 text-white/70 text-xs"
                >{{ server.region }}</Badge
              >
            </div>
          </div>

          <!-- Zone B: Card Body -->
          <div class="px-4 pt-3 pb-2">
            <p class="font-semibold truncate mb-2">{{ server.label }}</p>
            <div class="flex items-center justify-between text-sm mb-1.5">
              <span class="text-muted-foreground">{{
                $t("pages.public_servers.players")
              }}</span>
              <span
                :class="capacityClass(server)"
                class="font-mono font-medium"
              >
                {{ getDedicatedServerPlayers(server.id) }} /
                {{ server.max_players }}
              </span>
            </div>
            <div
              class="relative h-1.5 w-full overflow-hidden rounded-full bg-primary/20"
            >
              <div
                class="h-full rounded-full transition-all"
                :class="capacityBarClass(server)"
                :style="`width: ${capacityPercent(server)}%`"
              />
            </div>
          </div>

          <!-- Zone C: CTA Footer -->
          <div class="px-4 pb-4 pt-3">
            <div class="flex items-center gap-2">
              <div
                class="flex-1 [&>div]:w-full [&_a]:flex-1 [&_a_button]:w-full"
              >
                <QuickServerConnect :server="server" highlight />
              </div>
              <Button
                v-if="canManage"
                as-child
                variant="outline"
                size="icon"
                :title="$t('pages.public_servers.manage')"
              >
                <NuxtLink
                  :to="`/dedicated-servers/${server.id}`"
                  :aria-label="$t('pages.public_servers.manage')"
                >
                  <Settings2 class="h-4 w-4" />
                </NuxtLink>
              </Button>
            </div>
          </div>
        </AnimatedCard>
      </div>
    </div>
  </PageTransition>
</template>

<script lang="ts">
import { csRankIcon } from "~/utilities/csRank";

type PublicRankRow = {
  steam_id: string;
  name: string | null;
  points: number;
  skill_group: number;
  rank_name: string;
};

export default {
  data() {
    return {
      servers: undefined as any[] | undefined,
      serversByGame: {} as Record<string, Record<string, any[]>>,
      modeFilter: "all",
      lanServers: undefined as any[] | undefined,
      getDedicatedServerInfo: undefined as any[] | undefined,
      maps: undefined as any[] | undefined,
      loading: true,
      ranksLeaderboard: [] as PublicRankRow[],
    };
  },
  apollo: {
    getDedicatedServerInfo: {
      query: generateQuery({
        getDedicatedServerInfo: [
          {},
          {
            id: true,
            map: true,
            players: true,
            lastPing: true,
          },
        ],
      }),
      pollInterval: 60 * 1000,
    },
    maps: {
      query: generateQuery({
        maps: [{}, { name: true, patch: true }],
      }),
    },
    $subscribe: {
      servers: {
        query: generateSubscription({
          servers: [
            {
              where: {
                _and: [
                  {
                    _or: [
                      {
                        type: {
                          _neq: $("rankedType", "e_server_types_enum!"),
                        },
                      },
                      {
                        connection_string: {
                          _is_null: false,
                        },
                      },
                    ],
                  },
                  {
                    enabled: {
                      _eq: true,
                    },
                  },
                  {
                    connected: {
                      _eq: true,
                    },
                  },
                ],
              },
              order_by: [
                {
                  label: "asc" as any,
                },
              ],
            },
            {
              id: true,
              label: true,
              type: true,
              game: true,
              region: true,
              connected: true,
              connection_link: true,
              connection_string: true,
              max_players: true,
              game_mode: {
                slug: true,
                name: true,
                description: true,
              },
              server_region: {
                is_lan: true,
              },
            },
          ],
        }),
        variables: function () {
          return {
            rankedType: e_server_types_enum.Ranked,
          };
        },
        result: function ({ data }: { data: any }) {
          const nonLan = data.servers.filter(
            (server: any) => !server.server_region.is_lan,
          );
          this.servers = nonLan;
          this.serversByGame = nonLan.reduce(
            (acc: Record<string, Record<string, any[]>>, s: any) => {
              if (!acc[s.game]) acc[s.game] = {};
              (acc[s.game][s.type] = acc[s.game][s.type] || []).push(s);
              return acc;
            },
            {} as Record<string, Record<string, any[]>>,
          );
          this.lanServers = data.servers.filter(
            (server: any) => server.server_region.is_lan,
          );
          this.loading = false;
        },
      },
    },
  },
  computed: {
    modeFilters(): Array<{ key: string; label: string; count: number }> {
      const counts = new Map<string, { label: string; count: number }>();

      for (const server of this.servers as Array<Record<string, any>>) {
        const mode = server.game_mode;
        const key = mode?.slug ?? "vanilla";
        const label = mode?.name ?? this.$t("pages.public_servers.no_mode");
        const entry = counts.get(key) ?? { label: String(label), count: 0 };

        entry.count++;
        counts.set(key, entry);
      }

      if (counts.size === 0) {
        return [];
      }

      return [
        {
          key: "all",
          label: String(this.$t("pages.public_servers.all_modes")),
          count: (this.servers as Array<unknown>).length,
        },
        ...[...counts.entries()].map(([key, entry]) => ({
          key,
          label: entry.label,
          count: entry.count,
        })),
      ];
    },
  },
  mounted() {
    void this.loadRanksLeaderboard();
  },
  methods: {
    csRankIcon,
    async loadRanksLeaderboard() {
      try {
        const data = await $fetch<{ players: PublicRankRow[] }>(
          "/api/public-ranks",
          { query: { limit: 15 } },
        );
        this.ranksLeaderboard = data?.players || [];
      } catch {
        this.ranksLeaderboard = [];
      }
    },
    matchingMode(servers: Array<Record<string, any>>) {
      if (this.modeFilter === "all") {
        return servers;
      }

      return servers.filter(
        (server) => (server.game_mode?.slug ?? "vanilla") === this.modeFilter,
      );
    },
    gameLabel(game: string): string {
      const labels: Record<string, string> = {
        cs2: "Counter-Strike 2",
        csgo: "Counter-Strike: Global Offensive",
      };
      return labels[game] ?? game;
    },
    flattenGame(typeMap: Record<string, any[]>): any[] {
      return Object.values(typeMap).flat();
    },
    getDedicatedServerMap(id: string) {
      return this.getDedicatedServerInfo?.find((server) => server.id === id)
        ?.map;
    },
    getDedicatedServerPlayers(id: string) {
      return (
        this.getDedicatedServerInfo?.find((server) => server.id === id)
          ?.players || 0
      );
    },
    mapName(id: string): string {
      return this.getDedicatedServerMap(id) || "default";
    },
    capacityPercent(server: any): number {
      const players = this.getDedicatedServerPlayers(server.id);
      return server.max_players > 0
        ? Math.min(100, Math.round((players / server.max_players) * 100))
        : 0;
    },
    capacityClass(server: any): string {
      const pct = this.capacityPercent(server);
      if (pct >= 80) return "text-red-400";
      if (pct >= 50) return "text-yellow-400";
      return "text-green-400";
    },
    capacityBarClass(server: any): string {
      const pct = this.capacityPercent(server);
      if (pct >= 80) return "bg-red-400";
      if (pct >= 50) return "bg-yellow-400";
      return "bg-green-400";
    },
    onImgError(e: Event) {
      (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
    },
  },
};
</script>
