<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import { Skeleton } from "~/components/ui/skeleton";
import { eloTierColor } from "~/utils/eloTier";
import { loginLinks } from "~/utilities/loginLinks";
import {
  fetchTopPlayersByBestElo,
  type LandingEloPlayer,
} from "~/utils/landingBestElo";

const { locale } = useI18n();
const { client: apolloClient } = useApolloClient();

const isFa = computed(() =>
  String(locale.value || "").toLowerCase().startsWith("fa"),
);

const copy = computed(() => {
  if (isFa.value) {
    return {
      heading: "رنکینگ بازیکن‌ها.",
      body: "بالاترین Elo هر بازیکن در هر مودی (Competitive، Trios، Wingman یا Duel) — همان عددی که الان بیشترین است.",
      rank: "رنک",
      player: "بازیکن",
      elo: "Elo",
      emptyPlayers: "هنوز رتبه‌بندی‌ای نیست. اولین مچ‌ها را بازی کنید تا جدول پر شود.",
      viewAll: "مشاهده کامل لیدربورد",
    };
  }
  return {
    heading: "Rankings for individual players.",
    body: "Each player is ranked by their highest current Elo across Competitive, Trios, Wingman, and Duel.",
    rank: "Rank",
    player: "Player",
    elo: "Elo",
    emptyPlayers:
      "No rankings yet. Play your first matches and the board will fill in.",
    viewAll: "View full leaderboard",
  };
});

type PlayerRow = LandingEloPlayer & { rank: number };

const loadingPlayers = ref(true);
const players = ref<PlayerRow[]>([]);

async function fetchPlayers() {
  loadingPlayers.value = true;
  try {
    const rows = await fetchTopPlayersByBestElo(apolloClient as any, 10);
    players.value = rows.map((row, index): PlayerRow => ({
      ...row,
      rank: index + 1,
    }));
  } catch (error) {
    console.error("landing leaderboard fetch failed", error);
    players.value = [];
  } finally {
    loadingPlayers.value = false;
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
    <div class="mx-auto max-w-7xl px-4 py-14 sm:px-6 sm:py-20 lg:px-10">
      <h2
        class="m-0 max-w-4xl font-sans text-[clamp(1.5rem,5vw,2.85rem)] font-black uppercase leading-[1.05] tracking-[-0.02em] text-white"
      >
        {{ copy.heading }}
      </h2>
      <p
        class="mt-4 max-w-3xl text-[0.95rem] leading-relaxed text-white/55 sm:text-[1.05rem]"
      >
        {{ copy.body }}
      </p>

      <div
        class="mt-8 overflow-hidden rounded-xl border border-white/10 bg-[#161616] sm:mt-10"
      >
        <div
          class="grid grid-cols-[2.75rem_minmax(0,1fr)_4.5rem] gap-2 border-b border-white/10 bg-[#1c1c1c] px-3 py-3 font-sans text-[0.62rem] font-bold uppercase tracking-[0.12em] text-white/55 sm:grid-cols-[4rem_1fr_6rem] sm:gap-3 sm:px-5 sm:text-[0.68rem] sm:tracking-[0.14em]"
        >
          <span>{{ copy.rank }}</span>
          <span>{{ copy.player }}</span>
          <span class="text-right">{{ copy.elo }}</span>
        </div>

        <div v-if="loadingPlayers" class="space-y-3 p-3 sm:p-4">
          <div
            v-for="i in 8"
            :key="i"
            class="grid grid-cols-[2.75rem_minmax(0,1fr)_4.5rem] items-center gap-2 sm:grid-cols-[4rem_1fr_6rem] sm:gap-3"
          >
            <Skeleton class="h-5 w-6 bg-white/10" />
            <div class="flex min-w-0 items-center gap-3">
              <Skeleton class="h-9 w-9 shrink-0 rounded bg-white/10" />
              <Skeleton class="h-5 w-24 bg-white/10 sm:w-32" />
            </div>
            <Skeleton class="ml-auto h-5 w-12 bg-white/10 sm:w-14" />
          </div>
        </div>

        <p
          v-else-if="players.length === 0"
          class="px-4 py-10 text-center text-sm text-white/45 sm:px-5"
        >
          {{ copy.emptyPlayers }}
        </p>

        <ul v-else class="divide-y divide-white/10">
          <li v-for="entry in players" :key="entry.player_steam_id">
            <button
              type="button"
              class="grid w-full cursor-pointer grid-cols-[2.75rem_minmax(0,1fr)_4.5rem] items-center gap-2 px-3 py-3 text-left transition-colors hover:bg-white/[0.04] sm:grid-cols-[4rem_1fr_6rem] sm:gap-3 sm:px-5"
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
              <div class="min-w-0 overflow-hidden">
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
              </div>
              <span
                class="text-right font-mono text-xs font-semibold tabular-nums sm:text-sm"
                :style="{ color: eloTierColor(entry.value) || '#fff' }"
              >
                {{ Math.round(entry.value).toLocaleString() }}
              </span>
            </button>
          </li>
        </ul>

        <div class="border-t border-white/10 px-4 py-4 sm:px-5">
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
    </div>
  </section>
</template>
