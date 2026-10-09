<script setup lang="ts">
import { Button } from "@/components/ui/button";
import { Settings2, Radio, Users } from "lucide-vue-next";
import AnimatedFilters from "~/components/common/AnimatedFilters.vue";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PublicServerCard from "~/components/public-servers/PublicServerCard.vue";
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
      <template #description>{{
        $t("pages.public_servers.eyebrow")
      }}</template>
      <template #title>{{ $t("pages.public_servers.title") }}</template>
      <template #subtitle>{{
        $t("pages.public_servers.subtitle")
      }}</template>

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

  <!-- Fleet pulse strip -->
  <PageTransition :delay="60">
    <div
      v-if="!loading && servers && servers.length"
      class="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3"
    >
      <div
        class="rounded-lg border border-border/70 bg-card/50 px-4 py-3 backdrop-blur-sm"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground"
        >
          {{ $t("pages.public_servers.stat_servers") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-2xl font-bold tabular-nums text-foreground"
        >
          <Radio class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
          {{ servers.length }}
        </p>
      </div>
      <div
        class="rounded-lg border border-border/70 bg-card/50 px-4 py-3 backdrop-blur-sm"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground"
        >
          {{ $t("pages.public_servers.stat_players") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-2xl font-bold tabular-nums text-emerald-400"
        >
          <Users class="h-4 w-4" />
          {{ totalPlayers }}
        </p>
      </div>
      <div
        class="col-span-2 rounded-lg border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.06)] px-4 py-3 sm:col-span-1"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-[hsl(var(--tac-amber))]"
        >
          {{ $t("pages.public_servers.stat_live") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-sm font-semibold text-foreground"
        >
          <span class="relative flex h-2 w-2">
            <span
              class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-50"
            />
            <span
              class="relative inline-flex h-2 w-2 rounded-full bg-emerald-400"
            />
          </span>
          {{ $t("pages.public_servers.stat_live_hint") }}
        </p>
      </div>
    </div>
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
        <div
          v-if="loading"
          key="loading"
          class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3"
        >
          <div
            v-for="i in 3"
            :key="i"
            class="overflow-hidden rounded-xl border border-border/60"
          >
            <Skeleton class="h-48 w-full" />
            <div class="space-y-3 p-4">
              <Skeleton class="h-4 w-3/4" />
              <Skeleton class="h-2 w-full" />
              <Skeleton class="h-10 w-full" />
            </div>
          </div>
        </div>

        <Empty
          v-else-if="!servers || (servers as any[]).length === 0"
          key="empty"
          class="min-h-[220px]"
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

        <div v-else key="servers" class="space-y-8">
          <AnimatedFilters
            v-if="modeFilters.length > 2"
            v-model="modeFilter"
            square
            :options="modeFilters"
          />

          <div v-for="(gameServers, game) in serversByGame" :key="game">
            <div class="mb-5 flex items-center gap-3">
              <div
                class="h-4 w-0.5 shrink-0 rounded-full bg-[hsl(var(--tac-amber))]"
              />
              <span
                class="whitespace-nowrap text-xs font-bold uppercase tracking-[0.15em] text-muted-foreground"
              >
                {{ gameLabel(game) }}
              </span>
              <div class="h-px flex-1 bg-border" />
              <span
                class="font-mono text-[0.65rem] tabular-nums text-muted-foreground/70"
              >
                {{ matchingMode(flattenGame(gameServers)).length }}
              </span>
            </div>

            <div class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              <PublicServerCard
                v-for="(server, index) of matchingMode(
                  flattenGame(gameServers),
                )"
                :key="server.id"
                :server="server"
                :map-name="mapName(server.id)"
                :map-patch="mapPatch(server.id)"
                :players="getDedicatedServerPlayers(server.id)"
                :can-manage="canManage"
                show-mode
                class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
                :style="{ animationDelay: `${Math.min(index, 8) * 45}ms` }"
                @img-error="onImgError"
              />
            </div>
          </div>
        </div>
      </Transition>
    </div>
  </PageTransition>

  <PageTransition :delay="200">
    <div v-if="!loading && lanServers && lanServers.length > 0" class="mt-10">
      <div class="mb-5 flex items-center gap-3">
        <div
          class="h-4 w-0.5 shrink-0 rounded-full bg-muted-foreground/40"
        />
        <span
          class="whitespace-nowrap text-xs font-bold uppercase tracking-[0.15em] text-muted-foreground/60"
        >
          {{ $t("pages.public_servers.lan_servers_title") }}
        </span>
        <div class="h-px flex-1 bg-border" />
      </div>
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <PublicServerCard
          v-for="server of lanServers"
          :key="server.id"
          :server="server"
          :map-name="mapName(server.id)"
          :map-patch="mapPatch(server.id)"
          :players="getDedicatedServerPlayers(server.id)"
          :can-manage="canManage"
          @img-error="onImgError"
        />
      </div>
    </div>
  </PageTransition>
</template>

<script lang="ts">
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
                  // The Servers section's servers are listed there only.
                  { section_mode: { _is_null: true } } as any,
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
    totalPlayers(): number {
      if (!this.servers?.length) return 0;
      return (this.servers as any[]).reduce(
        (sum, s) => sum + this.getDedicatedServerPlayers(s.id),
        0,
      );
    },
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
  methods: {
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
    mapPatch(id: string): string | undefined {
      const name = this.getDedicatedServerMap(id);
      return this.maps?.find((m) => m.name === name)?.patch;
    },
    mapName(id: string): string {
      return this.getDedicatedServerMap(id) || "default";
    },
    onImgError(e: Event) {
      (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
    },
  },
};
</script>
