<script setup lang="ts">
import gql from "graphql-tag";
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import { Skeleton } from "~/components/ui/skeleton";
import { eloTierColor } from "~/utils/eloTier";
import { generateQuery } from "~/graphql/graphqlGen";
import { order_by } from "~/generated/zeus";
import { loginLinks } from "~/utilities/loginLinks";

const { locale } = useI18n();
const { client: apolloClient } = useApolloClient();

const isFa = computed(() =>
  String(locale.value || "").toLowerCase().startsWith("fa"),
);

const copy = computed(() => {
  if (isFa.value) {
    return {
      heading: "رنکینگ برای بازیکن‌ها و تیم‌ها.",
      body: "در رتبه‌بندی YGuard به‌عنوان بازیکن بالا برو و ببین کجای جدول ایستاده‌ای. هنوز سولو هستی؟ یک تیم بساز یا به تیم بپیوند و با هم رقابت کنید.",
      players: "بازیکن‌ها",
      teams: "تیم‌ها",
      rank: "رنک",
      player: "بازیکن",
      team: "تیم",
      elo: "Elo",
      members: "اعضا",
      emptyPlayers: "هنوز رتبه‌بندی‌ای نیست. اولین مچ‌ها را بازی کنید تا جدول پر شود.",
      emptyTeams: "هنوز تیمی ثبت نشده.",
      viewAll: "مشاهده کامل لیدربورد",
      viewTeams: "همه تیم‌ها",
    };
  }
  return {
    heading: "Rankings for both individual players and teams.",
    body: "Climb YGuard rankings as a player and see where you stand against other competitive players. Still solo? Create or join a team and compete together.",
    players: "Players",
    teams: "Teams",
    rank: "Rank",
    player: "Player",
    team: "Team",
    elo: "Elo",
    members: "Members",
    emptyPlayers:
      "No rankings yet. Play your first matches and the board will fill in.",
    emptyTeams: "No teams registered yet.",
    viewAll: "View full leaderboard",
    viewTeams: "Browse all teams",
  };
});

type Tab = "players" | "teams";
const tab = ref<Tab>("players");

type PlayerRow = {
  rank: number;
  player_steam_id: string;
  player_name: string;
  player_avatar_url: string | null;
  player_custom_avatar_url: string | null;
  player_country: string | null;
  value: number;
};

type TeamRow = {
  id: string;
  name: string;
  short_name: string | null;
  avatar_url: string | null;
  members: number;
};

const loadingPlayers = ref(true);
const loadingTeams = ref(false);
const players = ref<PlayerRow[]>([]);
const teams = ref<TeamRow[]>([]);
const teamsLoaded = ref(false);

const PLAYERS_QUERY = gql`
  query LandingLeaderboard(
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

async function fetchPlayers() {
  loadingPlayers.value = true;
  try {
    const { data } = await apolloClient.query({
      query: PLAYERS_QUERY,
      variables: {
        category: "elo",
        window_days: 0,
        match_type: "Competitive",
        exclude_tournaments: false,
        role: null,
        season_id: null,
        source: "overall",
        limit: 10,
        offset: 0,
        order_by: [{ value: "desc" }],
      },
      fetchPolicy: "network-only",
    });
    const rows = data?.get_leaderboard ?? [];
    players.value = rows.map((row: any, index: number): PlayerRow => ({
      ...row,
      rank: index + 1,
      value: Number(row.value),
    }));
  } catch (error) {
    console.error("landing leaderboard fetch failed", error);
    players.value = [];
  } finally {
    loadingPlayers.value = false;
  }
}

async function fetchTeams() {
  if (teamsLoaded.value) return;
  loadingTeams.value = true;
  try {
    const { data } = await apolloClient.query({
      query: generateQuery({
        teams: [
          {
            limit: 10,
            order_by: [{ name: order_by.asc }],
          },
          {
            id: true,
            name: true,
            short_name: true,
            avatar_url: true,
            team_rosters_aggregate: [{}, { aggregate: { count: true } }],
          },
        ],
      }),
      fetchPolicy: "network-only",
    });
    teams.value = (data?.teams ?? []).map(
      (t: any): TeamRow => ({
        id: t.id,
        name: t.name,
        short_name: t.short_name,
        avatar_url: t.avatar_url,
        members: Number(t.team_rosters_aggregate?.aggregate?.count ?? 0),
      }),
    );
    teamsLoaded.value = true;
  } catch (error) {
    console.error("landing teams fetch failed", error);
    teams.value = [];
  } finally {
    loadingTeams.value = false;
  }
}

function setTab(next: Tab) {
  tab.value = next;
  if (next === "teams") {
    void fetchTeams();
  }
}

function loginTo(path: string) {
  const dest = path.startsWith("/") ? path : `/${path}`;
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(window.location.origin + dest)}`;
}

onMounted(() => {
  void fetchPlayers();
});
</script>

<template>
  <section class="relative z-10 border-t border-white/10 bg-[#121212]">
    <div class="mx-auto max-w-7xl px-6 py-20 lg:px-10">
      <h2
        class="m-0 max-w-4xl font-sans text-[clamp(1.75rem,4vw,2.85rem)] font-black uppercase leading-[1.05] tracking-[-0.02em] text-white"
      >
        {{ copy.heading }}
      </h2>
      <p class="mt-4 max-w-3xl text-[1.05rem] leading-relaxed text-white/55">
        {{ copy.body }}
      </p>

      <div class="mt-10 flex items-center justify-end">
        <div
          class="inline-flex rounded-full border border-white/15 bg-black/40 p-1"
          role="tablist"
        >
          <button
            type="button"
            role="tab"
            class="rounded-full px-4 py-2 font-sans text-[0.72rem] font-black uppercase tracking-[0.12em] transition-colors"
            :class="
              tab === 'players'
                ? 'bg-[#aa0e19] text-white'
                : 'text-white/70 hover:text-white'
            "
            :aria-selected="tab === 'players'"
            @click="setTab('players')"
          >
            {{ copy.players }}
          </button>
          <button
            type="button"
            role="tab"
            class="rounded-full px-4 py-2 font-sans text-[0.72rem] font-black uppercase tracking-[0.12em] transition-colors"
            :class="
              tab === 'teams'
                ? 'bg-[#aa0e19] text-white'
                : 'text-white/70 hover:text-white'
            "
            :aria-selected="tab === 'teams'"
            @click="setTab('teams')"
          >
            {{ copy.teams }}
          </button>
        </div>
      </div>

      <!-- Players leaderboard (same source as /leaderboard Elo) -->
      <div v-if="tab === 'players'" class="mt-6 overflow-hidden rounded-xl border border-white/10 bg-[#161616]">
        <div
          class="grid grid-cols-[3.5rem_1fr_5.5rem] gap-3 border-b border-white/10 bg-[#1c1c1c] px-4 py-3 font-sans text-[0.68rem] font-bold uppercase tracking-[0.14em] text-white/55 sm:grid-cols-[4rem_1fr_6rem] sm:px-5"
        >
          <span>{{ copy.rank }}</span>
          <span>{{ copy.player }}</span>
          <span class="text-right">{{ copy.elo }}</span>
        </div>

        <div v-if="loadingPlayers" class="space-y-3 p-4">
          <div
            v-for="i in 8"
            :key="i"
            class="grid grid-cols-[3.5rem_1fr_5.5rem] items-center gap-3 sm:grid-cols-[4rem_1fr_6rem]"
          >
            <Skeleton class="h-5 w-6 bg-white/10" />
            <div class="flex items-center gap-3">
              <Skeleton class="h-9 w-9 rounded bg-white/10" />
              <Skeleton class="h-5 w-32 bg-white/10" />
            </div>
            <Skeleton class="ml-auto h-5 w-14 bg-white/10" />
          </div>
        </div>

        <p
          v-else-if="players.length === 0"
          class="px-5 py-10 text-center text-sm text-white/45"
        >
          {{ copy.emptyPlayers }}
        </p>

        <ul v-else class="divide-y divide-white/10">
          <li v-for="entry in players" :key="entry.player_steam_id">
            <button
              type="button"
              class="grid w-full cursor-pointer grid-cols-[3.5rem_1fr_5.5rem] items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-white/[0.04] sm:grid-cols-[4rem_1fr_6rem] sm:px-5"
              @click="loginTo(`/players/${entry.player_steam_id}`)"
            >
              <span
                class="font-mono text-sm font-bold tabular-nums"
                :class="{
                  'text-yellow-400': entry.rank === 1,
                  'text-gray-300': entry.rank === 2,
                  'text-amber-600': entry.rank === 3,
                  'text-white/45': entry.rank > 3,
                }"
              >
                {{ entry.rank }}
              </span>
              <PlayerDisplay
                :player="{
                  steam_id: entry.player_steam_id,
                  name: entry.player_name,
                  avatar_url: entry.player_avatar_url,
                  custom_avatar_url: entry.player_custom_avatar_url,
                  country: entry.player_country,
                }"
                :show-elo="false"
                :show-online="false"
                :show-role="false"
                :linkable="false"
                size="xs"
              />
              <span
                class="text-right font-mono text-sm font-semibold tabular-nums"
                :style="{ color: eloTierColor(entry.value) || '#fff' }"
              >
                {{ Math.round(entry.value).toLocaleString() }}
              </span>
            </button>
          </li>
        </ul>

        <div class="border-t border-white/10 px-5 py-4">
          <button
            type="button"
            class="inline-flex items-center gap-2 font-sans text-[0.75rem] font-black uppercase tracking-[0.14em] text-[#aa0e19] hover:text-white"
            @click="loginTo('/leaderboard')"
          >
            {{ copy.viewAll }}
            <span aria-hidden="true">→</span>
          </button>
        </div>
      </div>

      <!-- Teams -->
      <div v-else class="mt-6 overflow-hidden rounded-xl border border-white/10 bg-[#161616]">
        <div
          class="grid grid-cols-[3.5rem_1fr_5.5rem] gap-3 border-b border-white/10 bg-[#1c1c1c] px-4 py-3 font-sans text-[0.68rem] font-bold uppercase tracking-[0.14em] text-white/55 sm:grid-cols-[4rem_1fr_6rem] sm:px-5"
        >
          <span>{{ copy.rank }}</span>
          <span>{{ copy.team }}</span>
          <span class="text-right">{{ copy.members }}</span>
        </div>

        <div v-if="loadingTeams" class="space-y-3 p-4">
          <div
            v-for="i in 6"
            :key="i"
            class="grid grid-cols-[3.5rem_1fr_5.5rem] items-center gap-3 sm:grid-cols-[4rem_1fr_6rem]"
          >
            <Skeleton class="h-5 w-6 bg-white/10" />
            <Skeleton class="h-5 w-40 bg-white/10" />
            <Skeleton class="ml-auto h-5 w-10 bg-white/10" />
          </div>
        </div>

        <p
          v-else-if="teams.length === 0"
          class="px-5 py-10 text-center text-sm text-white/45"
        >
          {{ copy.emptyTeams }}
        </p>

        <ul v-else class="divide-y divide-white/10">
          <li v-for="(team, index) in teams" :key="team.id">
            <button
              type="button"
              class="grid w-full cursor-pointer grid-cols-[3.5rem_1fr_5.5rem] items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-white/[0.04] sm:grid-cols-[4rem_1fr_6rem] sm:px-5"
              @click="loginTo(`/teams/${team.id}`)"
            >
              <span
                class="font-mono text-sm font-bold tabular-nums text-white/45"
              >
                {{ index + 1 }}
              </span>
              <div class="flex min-w-0 items-center gap-3">
                <img
                  v-if="team.avatar_url"
                  :src="team.avatar_url"
                  alt=""
                  class="h-9 w-9 rounded object-cover"
                />
                <div
                  v-else
                  class="flex h-9 w-9 items-center justify-center rounded bg-white/10 font-sans text-xs font-bold text-[#aa0e19]"
                >
                  {{ (team.short_name || team.name).slice(0, 2).toUpperCase() }}
                </div>
                <span class="truncate font-sans text-sm font-semibold text-white">
                  {{ team.name }}
                </span>
              </div>
              <span
                class="text-right font-mono text-sm tabular-nums text-white/70"
              >
                {{ team.members }}
              </span>
            </button>
          </li>
        </ul>

        <div class="border-t border-white/10 px-5 py-4">
          <button
            type="button"
            class="inline-flex items-center gap-2 font-sans text-[0.75rem] font-black uppercase tracking-[0.14em] text-[#aa0e19] hover:text-white"
            @click="loginTo('/teams')"
          >
            {{ copy.viewTeams }}
            <span aria-hidden="true">→</span>
          </button>
        </div>
      </div>
    </div>
  </section>
</template>
