<script setup lang="ts">
import gql from "graphql-tag";
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { useBranding } from "~/composables/useBranding";
import { loginLinks } from "~/utilities/loginLinks";
import LandingRankings from "~/components/landing/LandingRankings.vue";
import { eloTierColor } from "~/utils/eloTier";

useHead({
  link: [
    {
      rel: "stylesheet",
      href: "https://cdn.jsdelivr.net/npm/geist@1.3.1/dist/fonts/geist-sans/style.min.css",
    },
  ],
});

const { copy: seoPage } = useSiteSeo("home");

const { locale } = useI18n();
const { brandName, logoUrl } = useBranding();
const { client: apolloClient } = useApolloClient();

const isFa = computed(() =>
  String(locale.value || "").toLowerCase().startsWith("fa"),
);
const displayBrand = computed(() => brandName.value || "YGuard");

const copy = computed(() => {
  if (isFa.value) {
    return {
      challengeYour: "چالش بده",
      subtitle: `بازیکن‌ها برای بهترین تجربه رقابتی کانتر استرایک ۲ به ${displayBrand.value} (وای گارد) می‌آیند. مچ‌میکینگ رنک، سرور پابلیک CS2، تورنمنت و آنتی‌چیت اختصاصی.`,
      cta: "همین حالا بازی کن",
      onlineSuffix: "بازیکن آنلاین الان",
      words: ["بازی", "ایم", "مهارت", "رنک", "محدودیت", "ذهن", "تیم"],
      elo: "Elo",
      featuresHeading: "بیش از یک مچ‌میکینگ.",
      featuresSub: `${displayBrand.value} خانه رقابت CS2 است — صف رنک، مچ زنده، سرور پابلیک، اینونتوری و ابزارهای کامیونیتی.`,
      features: [
        {
          title: "مچ‌میکینگ رنک",
          body: "سولو یا با تیم. Elo بگیر در Competitive، Wingman و Duel.",
          cta: "ورود به صف",
          redirect: "/play",
        },
        {
          title: "تماشا و هایلایت",
          body: "مچ‌های زنده، استریم و کلیپ‌های CS2 را دنبال کن.",
          cta: "تماشا",
          redirect: "/watch",
        },
        {
          title: "اینونتوری",
          body: "لوداوت بساز، اسکین امتحان کن و وارد سرور شو.",
          cta: "اینونتوری",
          redirect: "/apps/inventory",
        },
        {
          title: "لیدربورد",
          body: "رنک بگیر و جایگاهت را بین بازیکن‌های وای گارد ببین.",
          cta: "رتبه‌بندی",
          redirect: "/leaderboard",
        },
      ],
      seoHeading: "وای گارد چیست؟",
      seoBody: [
        `${displayBrand.value} (وای گارد / وایگارد) پلتفرم رقابتی کانتر استرایک ۲ برای بازیکن‌های فارسی‌زبان است: مچ‌میکینگ با سیستم Elo، سرورهای پابلیک و اختصاصی CS2، اسکرم، تورنمنت و آنتی‌چیت اختصاصی YGuard AC.`,
        "اگر دنبال رنک سی اس، سرور سی اس ۲ ایران، صف Competitive شبیه فیس‌ایت یا تجربه تمیز بدون تقلب هستی، وای گارد همان جاست.",
      ],
      seoLinks: [
        { to: "/cs2", label: "مچ‌میکینگ و رنک CS2" },
        { to: "/anticheat", label: "آنتی‌چیت وای گارد" },
        { to: "/servers", label: "سرور پابلیک CS2" },
      ],
      closingTitle: "آماده چالش روی",
      closingCta: "ورود با استیم",
    };
  }

  return {
    challengeYour: "Challenge your",
    subtitle: `Players come to ${displayBrand.value} for competitive Counter-Strike 2 at its best. Grind matchmaking, climb Elo, and prove yourself with no ceiling on how far you can go.`,
    cta: "Play now",
    onlineSuffix: "players online right now",
    words: ["game", "aim", "skill", "rank", "limits", "mind", "team"],
    elo: "Elo",
    featuresHeading: "More than matchmaking.",
    featuresSub: `${displayBrand.value} is a home for every ambition — competitive queues, live matches, inventory, and community tools.`,
    features: [
      {
        title: "Matchmaking",
        body: "Solo or stack. Grind Elo in Competitive, Wingman, or Duel.",
        cta: "Play now",
        redirect: "/play",
      },
      {
        title: "Watch",
        body: "Follow live matches, streams, and highlights as they happen.",
        cta: "Watch live",
        redirect: "/watch",
      },
      {
        title: "Inventory",
        body: "Build your loadout, try skins, and take your setup into servers.",
        cta: "Open inventory",
        redirect: "/apps/inventory",
      },
      {
        title: "Leaderboard",
        body: "Climb the rankings and see where you stand against everyone else.",
        cta: "View rankings",
        redirect: "/leaderboard",
      },
    ],
    seoHeading: "What is YGuard?",
    seoBody: [
      `${displayBrand.value} is a competitive Counter-Strike 2 platform: ranked matchmaking with Elo, public and dedicated CS2 servers, scrims, tournaments, and YGuard Anti-Cheat.`,
      "Looking for CS2 ranked queues, Iran-friendly servers, or a clean Faceit-style grind? Start on YGuard.",
    ],
    seoLinks: [
      { to: "/cs2", label: "CS2 matchmaking & ranked" },
      { to: "/anticheat", label: "YGuard Anti-Cheat" },
      { to: "/servers", label: "CS2 public servers" },
    ],
    closingTitle: "Ready to challenge your game on",
    closingCta: "Play now",
  };
});

type HeroPlayer = {
  rank: number;
  name: string;
  avatar: string | null;
  elo: number;
};

const wordIndex = ref(0);
const ONLINE_MIN = 100;
const ONLINE_MAX = 200;
const onlineCount = ref(
  ONLINE_MIN + Math.floor(Math.random() * (ONLINE_MAX - ONLINE_MIN + 1)),
);
const heroPlayers = ref<HeroPlayer[]>([]);
let wordTimer: ReturnType<typeof setInterval> | undefined;
let onlineTimer: ReturnType<typeof setInterval> | undefined;

const TOP_PLAYERS_QUERY = gql`
  query LandingHeroPlayers(
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
      player_name
      player_avatar_url
      player_custom_avatar_url
      value
    }
  }
`;

function tickOnlineCount() {
  // Soft drift ±1–5 within 100–200 so the counter feels live.
  const step = 1 + Math.floor(Math.random() * 5);
  const dir = Math.random() < 0.5 ? -1 : 1;
  let next = onlineCount.value + dir * step;
  if (next < ONLINE_MIN) next = ONLINE_MIN + Math.floor(Math.random() * 8);
  if (next > ONLINE_MAX) next = ONLINE_MAX - Math.floor(Math.random() * 8);
  onlineCount.value = next;
}

async function fetchHeroPlayers() {
  try {
    const { data } = await apolloClient.query({
      query: TOP_PLAYERS_QUERY,
      variables: {
        category: "elo",
        window_days: 0,
        match_type: "Competitive",
        exclude_tournaments: false,
        role: null,
        season_id: null,
        source: "overall",
        limit: 3,
        offset: 0,
        order_by: [{ value: "desc" }],
      },
      fetchPolicy: "network-only",
    });
    const rows = data?.get_leaderboard ?? [];
    heroPlayers.value = rows.map((row: any, index: number): HeroPlayer => ({
      rank: index + 1,
      name: row.player_name || "Player",
      avatar: row.player_custom_avatar_url || row.player_avatar_url || null,
      elo: Number(row.value) || 0,
    }));
  } catch (error) {
    console.error("landing hero players fetch failed", error);
    heroPlayers.value = [];
  }
}

onMounted(() => {
  wordTimer = setInterval(() => {
    wordIndex.value = (wordIndex.value + 1) % copy.value.words.length;
  }, 2200);
  void fetchHeroPlayers();
  onlineTimer = setInterval(tickOnlineCount, 2800);
});

onBeforeUnmount(() => {
  if (wordTimer) clearInterval(wordTimer);
  if (onlineTimer) clearInterval(onlineTimer);
});

const activeWord = computed(
  () => copy.value.words[wordIndex.value] ?? copy.value.words[0],
);

/** Card order: #2 left, #1 center (featured), #3 right — Faceit-style podium. */
const heroCards = computed(() => {
  const [first, second, third] = heroPlayers.value;
  return [
    { slot: "left" as const, player: second ?? null },
    { slot: "you" as const, player: first ?? null },
    { slot: "right" as const, player: third ?? null },
  ];
});

function loginWithSteam(redirectPath = "/play") {
  const dest = redirectPath.startsWith("/")
    ? redirectPath
    : `/${redirectPath}`;
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(window.location.origin + dest)}`;
}

function avatarFallback(name: string) {
  return (name || "?").slice(0, 1).toUpperCase();
}
</script>

<template>
  <div class="landing-root overflow-x-hidden" :dir="isFa ? 'rtl' : 'ltr'">
    <!-- Faceit hero: #ccc + official light pattern cover -->
    <section class="landing-hero relative isolate overflow-hidden text-[#060606]">
      <div
        class="relative z-10 mx-auto grid min-h-[min(100svh,920px)] max-w-7xl items-center gap-16 px-6 py-16 lg:grid-cols-[1fr_1fr] lg:gap-24 lg:px-10 lg:py-20"
      >
        <div class="flex flex-col items-start gap-7">
          <div class="flex items-center gap-3">
            <img
              v-if="logoUrl"
              :src="logoUrl"
              :alt="displayBrand"
              class="h-9 w-9 object-contain"
            />
            <NuxtImg
              v-else
              src="/favicon/64.png"
              :alt="displayBrand"
              class="h-9 w-9 object-contain"
            />
            <span
              class="font-sans text-[1.2rem] font-black uppercase tracking-[0.12em] text-[#060606]"
            >
              {{ displayBrand }}
            </span>
          </div>

          <h1
            class="landing-hero-title m-0 font-black uppercase leading-[0.92] tracking-[-0.03em] text-[#060606]"
          >
            <span class="block whitespace-nowrap">{{ copy.challengeYour }}</span>
            <span
              class="relative block min-h-[1.05em] whitespace-nowrap text-[#aa0e19]"
              :key="activeWord"
            >
              {{ activeWord }}
            </span>
          </h1>

          <p
            class="m-0 max-w-xl text-[1.05rem] leading-[1.45] text-[#4a4a4a] sm:text-[1.125rem]"
          >
            {{ copy.subtitle }}
          </p>

          <div class="flex flex-wrap items-center gap-4 pt-1">
            <button
              type="button"
              class="inline-flex items-center gap-2 rounded-[4px] bg-[#aa0e19] px-6 py-4 font-sans text-[0.9rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px"
              @click="loginWithSteam('/play')"
            >
              <span
                aria-hidden="true"
                class="inline-block h-0 w-0 border-y-[5px] border-y-transparent border-l-[8px] border-l-[#060606]"
              ></span>
              {{ copy.cta }}
            </button>

            <!-- Faceit OnlineTextContainer: 9px #05ff00 dot + bold count + regular label -->
            <div
              class="landing-online inline-flex items-center"
              aria-live="polite"
            >
              <span aria-hidden="true" class="landing-online__dot"></span>
              <span
                class="landing-online__count"
                :key="onlineCount"
              >{{ onlineCount.toLocaleString("en-US") }}</span>
              <span class="landing-online__label">{{
                copy.onlineSuffix
              }}</span>
            </div>
          </div>
        </div>

        <div
          class="relative mx-auto flex w-full max-w-md items-end justify-center gap-3 sm:max-w-lg lg:max-w-none lg:justify-end"
        >
          <div
            v-for="card in heroCards"
            :key="card.slot"
            class="landing-card flex flex-col items-center justify-center gap-3 rounded-2xl"
            :class="{
              'landing-card--left h-[280px] w-[32%] max-w-[160px] bg-[#1a2332] sm:h-[340px]':
                card.slot === 'left',
              'landing-card--you relative z-10 h-[320px] w-[38%] max-w-[190px] gap-4 border border-[#aa0e19]/35 bg-[#1a1a1a] shadow-[0_18px_40px_rgba(0,0,0,0.28)] sm:h-[400px]':
                card.slot === 'you',
              'landing-card--right h-[280px] w-[32%] max-w-[160px] bg-[#1f1a12] sm:h-[340px]':
                card.slot === 'right',
            }"
          >
            <template v-if="card.player">
              <div
                class="relative"
                :class="
                  card.slot === 'you'
                    ? 'h-20 w-20 sm:h-24 sm:w-24'
                    : 'h-16 w-16 sm:h-20 sm:w-20'
                "
              >
                <img
                  v-if="card.player.avatar"
                  :src="card.player.avatar"
                  :alt="card.player.name"
                  class="h-full w-full rounded-full object-cover"
                  :class="
                    card.slot === 'you'
                      ? 'border-2 border-[#aa0e19]/55'
                      : 'border border-white/15'
                  "
                />
                <div
                  v-else
                  class="flex h-full w-full items-center justify-center rounded-full bg-white/10 font-sans text-xl font-black text-[#aa0e19]"
                  :class="
                    card.slot === 'you' ? 'border-2 border-[#aa0e19]/55' : ''
                  "
                >
                  {{ avatarFallback(card.player.name) }}
                </div>
                <span
                  class="absolute -bottom-1 -right-1 flex h-6 w-6 items-center justify-center rounded-full bg-[#aa0e19] font-sans text-[0.65rem] font-black text-[#060606]"
                >
                  {{ card.player.rank }}
                </span>
              </div>
              <span
                class="max-w-[90%] truncate px-2 text-center font-sans text-[0.75rem] font-bold tracking-[0.04em] text-white"
                :class="card.slot === 'you' ? 'text-[0.85rem]' : ''"
              >
                {{ card.player.name }}
              </span>
              <span
                class="font-mono text-[0.8rem] font-semibold tabular-nums"
                :style="{
                  color: eloTierColor(card.player.elo) || '#aa0e19',
                }"
              >
                {{ Math.round(card.player.elo).toLocaleString() }}
                <span class="ms-1 text-[0.65rem] uppercase text-white/45">{{
                  copy.elo
                }}</span>
              </span>
            </template>
            <template v-else>
              <div
                class="flex items-center justify-center rounded-full bg-white/10"
                :class="
                  card.slot === 'you'
                    ? 'h-20 w-20 sm:h-24 sm:w-24'
                    : 'h-16 w-16 sm:h-20 sm:w-20'
                "
              >
                <img
                  v-if="logoUrl"
                  :src="logoUrl"
                  alt=""
                  class="h-10 w-10 object-contain opacity-70"
                />
                <NuxtImg
                  v-else
                  src="/favicon/64.png"
                  alt=""
                  class="h-10 w-10 object-contain opacity-70"
                />
              </div>
              <span
                class="px-2 text-center font-sans text-[0.7rem] font-bold uppercase tracking-[0.12em] text-white/50"
              >
                {{ displayBrand }}
              </span>
            </template>
          </div>
        </div>
      </div>
    </section>

    <!-- Dark sections below (Faceit page body) -->
    <section class="border-t border-white/10 bg-[#121212] text-white">
      <div class="mx-auto max-w-7xl px-6 py-20 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.75rem,4vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.featuresHeading }}
        </h2>
        <p class="mt-4 max-w-2xl text-[1.05rem] leading-relaxed text-white/55">
          {{ copy.featuresSub }}
        </p>

        <div
          class="mt-12 grid gap-px overflow-hidden rounded-xl bg-white/10 sm:grid-cols-2"
        >
          <div
            v-for="feature in copy.features"
            :key="feature.title"
            class="flex flex-col gap-4 bg-[#161616] p-7 sm:p-8"
          >
            <h3
              class="m-0 font-sans text-[1.05rem] font-black uppercase tracking-[0.08em]"
            >
              {{ feature.title }}
            </h3>
            <p class="m-0 flex-1 text-[0.95rem] leading-relaxed text-white/55">
              {{ feature.body }}
            </p>
            <button
              type="button"
              class="inline-flex w-fit items-center gap-2 rounded-[4px] border border-[#aa0e19] px-4 py-2.5 font-sans text-[0.72rem] font-black uppercase tracking-[0.12em] text-[#aa0e19] transition-colors hover:bg-[#aa0e19] hover:text-[#060606]"
              @click="loginWithSteam(feature.redirect)"
            >
              {{ feature.cta }}
            </button>
          </div>
        </div>
      </div>
    </section>

    <LandingRankings />

    <!-- Crawlable keyword section for Persian CS / YGuard SEO -->
    <section class="border-t border-white/10 bg-[#0e0e0e] text-white">
      <div class="mx-auto max-w-7xl px-6 py-16 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.4rem,3vw,2rem)] font-black leading-snug tracking-[-0.02em]"
        >
          {{ copy.seoHeading }}
        </h2>
        <p
          v-for="(para, i) in copy.seoBody"
          :key="i"
          class="mt-4 max-w-3xl text-[1rem] leading-relaxed text-white/60"
        >
          {{ para }}
        </p>
        <nav
          class="mt-8 flex flex-wrap gap-3"
          :aria-label="isFa ? 'لینک‌های وای گارد' : 'YGuard topics'"
        >
          <NuxtLink
            v-for="link in copy.seoLinks"
            :key="link.to"
            :to="link.to"
            class="rounded-[4px] border border-white/15 px-4 py-2 text-[0.8rem] font-bold text-white/80 transition-colors hover:border-[#aa0e19] hover:text-[#aa0e19]"
          >
            {{ link.label }}
          </NuxtLink>
        </nav>
        <p class="sr-only">{{ seoPage.h1 }}</p>
      </div>
    </section>

    <section class="border-t border-white/10 bg-[#121212] text-white">
      <div
        class="mx-auto flex max-w-7xl flex-col items-start gap-6 px-6 py-20 lg:flex-row lg:items-center lg:justify-between lg:px-10"
      >
        <h2
          class="m-0 max-w-xl font-sans text-[clamp(1.75rem,4vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.closingTitle }}
          <span class="text-[#aa0e19]">{{ displayBrand }}</span
          >?
        </h2>
        <button
          type="button"
          class="inline-flex items-center gap-2 rounded-[4px] bg-[#aa0e19] px-7 py-4 font-sans text-[0.9rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px"
          @click="loginWithSteam('/play')"
        >
          <span
            aria-hidden="true"
            class="inline-block h-0 w-0 border-y-[5px] border-y-transparent border-l-[8px] border-l-[#060606]"
          ></span>
          {{ copy.closingCta }}
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.landing-root {
  --landing-accent: #aa0e19;
  font-family: "Geist Sans", geistSans, ui-sans-serif, system-ui, sans-serif;
}

.landing-hero {
  background-color: #cccccc;
  background-image: url("/img/landing-hero-bg.avif");
  background-repeat: no-repeat;
  background-position: 50% 50%;
  background-size: cover;
  min-height: 600px;
}

.landing-hero-title {
  font-family: "Geist Sans", geistSans, ui-sans-serif, system-ui, sans-serif;
  font-size: clamp(2.75rem, 7.2vw, 4.85rem);
}

.landing-online {
  font-family: "Geist Sans", geistSans, ui-sans-serif, system-ui, sans-serif;
}

.landing-online__dot {
  display: block;
  width: 9px;
  height: 9px;
  margin-inline-end: 8px;
  flex-shrink: 0;
  border-radius: 50%;
  background-color: #05ff00;
}

.landing-online__count {
  display: block;
  margin-inline-end: 4px;
  font-size: 14px;
  font-weight: 700;
  line-height: 20px;
  letter-spacing: 0.28px;
  color: #060606;
  animation: landing-online-tick 0.35s ease;
}

.landing-online__label {
  display: block;
  font-size: 14px;
  font-weight: 400;
  line-height: 20px;
  letter-spacing: 0.28px;
  color: #060606;
}

@keyframes landing-online-tick {
  0% {
    opacity: 0.35;
    transform: translateY(2px);
  }
  100% {
    opacity: 1;
    transform: translateY(0);
  }
}

.landing-card {
  animation: landing-float 5.5s ease-in-out infinite;
}
.landing-card--left {
  animation-delay: 0s;
}
.landing-card--you {
  animation-delay: 0.4s;
}
.landing-card--right {
  animation-delay: 0.8s;
}

@keyframes landing-float {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

@media (prefers-reduced-motion: reduce) {
  .landing-card {
    animation: none;
  }
  .landing-online__count {
    animation: none;
  }
}
</style>
