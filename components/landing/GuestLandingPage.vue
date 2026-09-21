<script setup lang="ts">
import {
  computed,
  defineAsyncComponent,
  onBeforeUnmount,
  onMounted,
  ref,
} from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { useBranding } from "~/composables/useBranding";
import { loginLinks } from "~/utilities/loginLinks";
import { eloTierColor } from "~/utils/eloTier";

// Below-fold: keep out of the landing critical chunk (TBT / Speed Index).
const LandingRankings = defineAsyncComponent(() =>
  import("~/components/landing/LandingRankings.vue"),
);

useHead({
  link: [
    {
      rel: "preload",
      as: "image",
      href: "/img/landing-hero-bg.avif",
      type: "image/avif",
      fetchpriority: "high",
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
  matchType: string;
};

const wordIndex = ref(0);
const ONLINE_MIN = 100;
const ONLINE_MAX = 200;
const onlineCount = ref(
  ONLINE_MIN + Math.floor(Math.random() * (ONLINE_MAX - ONLINE_MIN + 1)),
);
const heroPlayers = ref<HeroPlayer[]>([]);
const motionReady = ref(false);
const geistReady = ref(false);
const showRankings = ref(false);
let wordTimer: ReturnType<typeof setInterval> | undefined;
let onlineTimer: ReturnType<typeof setInterval> | undefined;
let rankingsObserver: IntersectionObserver | undefined;

function tickOnlineCount() {
  // Soft drift ±1–5 within 100–200 so the counter feels live.
  const step = 1 + Math.floor(Math.random() * 5);
  const dir = Math.random() < 0.5 ? -1 : 1;
  let next = onlineCount.value + dir * step;
  if (next < ONLINE_MIN) next = ONLINE_MIN + Math.floor(Math.random() * 8);
  if (next > ONLINE_MAX) next = ONLINE_MAX - Math.floor(Math.random() * 8);
  onlineCount.value = next;
}

function whenIdle(run: () => void, timeout = 2500) {
  if (typeof window === "undefined") return;
  if ("requestIdleCallback" in window) {
    window.requestIdleCallback(run, { timeout });
    return;
  }
  setTimeout(run, Math.min(timeout, 800));
}

function loadGeistFont() {
  const href =
    "https://cdn.jsdelivr.net/npm/geist@1.3.1/dist/fonts/geist-sans/style.min.css";
  if (document.querySelector(`link[href="${href}"]`)) {
    geistReady.value = true;
    return;
  }
  const link = document.createElement("link");
  link.rel = "stylesheet";
  link.href = href;
  link.media = "print";
  link.onload = () => {
    link.media = "all";
    geistReady.value = true;
  };
  document.head.appendChild(link);
}

async function fetchHeroPlayers() {
  try {
    const { fetchTopPlayersByBestElo } = await import("~/utils/landingBestElo");
    const rows = await fetchTopPlayersByBestElo(apolloClient as any, 3);
    heroPlayers.value = rows.map(
      (row, index): HeroPlayer => ({
        rank: index + 1,
        name: row.player_name || "Player",
        avatar: row.player_custom_avatar_url || row.player_avatar_url || null,
        elo: row.value,
        matchType: row.match_type,
      }),
    );
  } catch (error) {
    console.error("landing hero players fetch failed", error);
    heroPlayers.value = [];
  }
}

onMounted(() => {
  // First paint stays on system fonts + static hero; defer extras.
  whenIdle(() => {
    loadGeistFont();
    motionReady.value = true;
    void fetchHeroPlayers();
    wordTimer = setInterval(() => {
      wordIndex.value = (wordIndex.value + 1) % copy.value.words.length;
    }, 2200);
    onlineTimer = setInterval(tickOnlineCount, 2800);
  }, 2000);

  const sentinel = document.getElementById("landing-rankings-sentinel");
  if (sentinel && "IntersectionObserver" in window) {
    rankingsObserver = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          showRankings.value = true;
          rankingsObserver?.disconnect();
        }
      },
      { rootMargin: "240px 0px" },
    );
    rankingsObserver.observe(sentinel);
  } else {
    whenIdle(() => {
      showRankings.value = true;
    }, 4000);
  }
});

onBeforeUnmount(() => {
  if (wordTimer) clearInterval(wordTimer);
  if (onlineTimer) clearInterval(onlineTimer);
  rankingsObserver?.disconnect();
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
  <div
    class="landing-root overflow-x-hidden"
    :class="{
      'landing-root--motion': motionReady,
      'landing-root--geist': geistReady,
    }"
    :dir="isFa ? 'rtl' : 'ltr'"
  >
    <!-- Faceit hero: #ccc + official light pattern cover -->
    <section class="landing-hero relative isolate overflow-hidden text-[#060606]">
      <img
        class="landing-hero__bg"
        src="/img/landing-hero-bg.avif"
        alt=""
        width="1920"
        height="1080"
        decoding="async"
        fetchpriority="high"
      />
      <div
        class="relative z-10 mx-auto grid min-h-[min(100svh,920px)] max-w-7xl items-center gap-10 px-4 py-10 sm:gap-14 sm:px-6 sm:py-16 lg:grid-cols-[1fr_1fr] lg:gap-24 lg:px-10 lg:py-20"
      >
        <div class="flex flex-col items-start gap-5 sm:gap-7">
          <div class="flex items-center gap-3">
            <img
              v-if="logoUrl"
              :src="logoUrl"
              :alt="displayBrand"
              class="h-8 w-8 object-contain sm:h-9 sm:w-9"
            />
            <NuxtImg
              v-else
              src="/favicon/64.png"
              :alt="displayBrand"
              class="h-8 w-8 object-contain sm:h-9 sm:w-9"
            />
            <span
              class="font-sans text-[1rem] font-black uppercase tracking-[0.12em] text-[#060606] sm:text-[1.2rem]"
            >
              {{ displayBrand }}
            </span>
          </div>

          <h1
            class="landing-hero-title m-0 font-black uppercase leading-[0.92] tracking-[-0.03em] text-[#060606]"
          >
            <span class="block">{{ copy.challengeYour }}</span>
            <span
              class="relative mt-1 block min-h-[1.05em] text-[#aa0e19]"
              :key="activeWord"
            >
              {{ activeWord }}
            </span>
          </h1>

          <p
            class="m-0 max-w-xl text-[0.95rem] leading-[1.5] text-[#4a4a4a] sm:text-[1.125rem] sm:leading-[1.45]"
          >
            {{ copy.subtitle }}
          </p>

          <div
            class="flex w-full flex-col items-stretch gap-3 pt-1 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center sm:gap-4"
          >
            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 rounded-[4px] bg-[#aa0e19] px-5 py-3.5 font-sans text-[0.85rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px sm:px-6 sm:py-4 sm:text-[0.9rem]"
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
              class="landing-online inline-flex flex-wrap items-center justify-center sm:justify-start"
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
          class="landing-podium relative mx-auto flex w-full max-w-[22rem] items-end justify-center gap-2 sm:max-w-lg sm:gap-3 lg:mx-0 lg:max-w-none lg:justify-end"
        >
          <div
            v-for="card in heroCards"
            :key="card.slot"
            class="landing-card flex flex-col items-center justify-center gap-2 rounded-2xl sm:gap-3"
            :class="{
              'landing-card--left h-[200px] w-[30%] max-w-[120px] bg-[#1a2332] sm:h-[340px] sm:max-w-[160px]':
                card.slot === 'left',
              'landing-card--you relative z-10 h-[240px] w-[36%] max-w-[140px] gap-3 border border-[#aa0e19]/35 bg-[#1a1a1a] shadow-[0_18px_40px_rgba(0,0,0,0.28)] sm:h-[400px] sm:max-w-[190px] sm:gap-4':
                card.slot === 'you',
              'landing-card--right h-[200px] w-[30%] max-w-[120px] bg-[#1f1a12] sm:h-[340px] sm:max-w-[160px]':
                card.slot === 'right',
            }"
          >
            <template v-if="card.player">
              <div
                class="relative"
                :class="
                  card.slot === 'you'
                    ? 'h-14 w-14 sm:h-24 sm:w-24'
                    : 'h-12 w-12 sm:h-20 sm:w-20'
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
                  class="flex h-full w-full items-center justify-center rounded-full bg-white/10 font-sans text-lg font-black text-[#aa0e19] sm:text-xl"
                  :class="
                    card.slot === 'you' ? 'border-2 border-[#aa0e19]/55' : ''
                  "
                >
                  {{ avatarFallback(card.player.name) }}
                </div>
                <span
                  class="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-[#aa0e19] font-sans text-[0.6rem] font-black text-[#060606] sm:h-6 sm:w-6 sm:text-[0.65rem]"
                >
                  {{ card.player.rank }}
                </span>
              </div>
              <span
                class="max-w-[90%] truncate px-1.5 text-center font-sans text-[0.65rem] font-bold tracking-[0.04em] text-white sm:px-2 sm:text-[0.75rem]"
                :class="card.slot === 'you' ? 'sm:text-[0.85rem]' : ''"
              >
                {{ card.player.name }}
              </span>
              <span
                class="font-mono text-[0.7rem] font-semibold tabular-nums sm:text-[0.8rem]"
                :style="{
                  color: eloTierColor(card.player.elo) || '#aa0e19',
                }"
              >
                {{ Math.round(card.player.elo).toLocaleString() }}
                <span class="ms-0.5 text-[0.55rem] uppercase text-white/45 sm:ms-1 sm:text-[0.65rem]">{{
                  copy.elo
                }}</span>
              </span>
            </template>
            <template v-else>
              <div
                class="flex items-center justify-center rounded-full bg-white/10"
                :class="
                  card.slot === 'you'
                    ? 'h-14 w-14 sm:h-24 sm:w-24'
                    : 'h-12 w-12 sm:h-20 sm:w-20'
                "
              >
                <img
                  v-if="logoUrl"
                  :src="logoUrl"
                  alt=""
                  class="h-8 w-8 object-contain opacity-70 sm:h-10 sm:w-10"
                />
                <NuxtImg
                  v-else
                  src="/favicon/64.png"
                  alt=""
                  class="h-8 w-8 object-contain opacity-70 sm:h-10 sm:w-10"
                />
              </div>
              <span
                class="px-2 text-center font-sans text-[0.65rem] font-bold uppercase tracking-[0.12em] text-white/50 sm:text-[0.7rem]"
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
      <div class="mx-auto max-w-7xl px-4 py-14 sm:px-6 sm:py-20 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.5rem,5vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.featuresHeading }}
        </h2>
        <p class="mt-4 max-w-2xl text-[0.95rem] leading-relaxed text-white/55 sm:text-[1.05rem]">
          {{ copy.featuresSub }}
        </p>

        <div
          class="mt-10 grid gap-px overflow-hidden rounded-xl bg-white/10 sm:mt-12 sm:grid-cols-2"
        >
          <div
            v-for="feature in copy.features"
            :key="feature.title"
            class="flex flex-col gap-4 bg-[#161616] p-5 sm:p-8"
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

    <div id="landing-rankings-sentinel" class="h-px w-full" aria-hidden="true" />
    <LandingRankings v-if="showRankings" />

    <!-- Crawlable keyword section for Persian CS / YGuard SEO -->
    <section class="border-t border-white/10 bg-[#0e0e0e] text-white">
      <div class="mx-auto max-w-7xl px-4 py-12 sm:px-6 sm:py-16 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.25rem,4vw,2rem)] font-black leading-snug tracking-[-0.02em]"
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
        class="mx-auto flex max-w-7xl flex-col items-stretch gap-6 px-4 py-14 sm:items-start sm:px-6 sm:py-20 lg:flex-row lg:items-center lg:justify-between lg:px-10"
      >
        <h2
          class="m-0 max-w-xl font-sans text-[clamp(1.5rem,5vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.closingTitle }}
          <span class="text-[#aa0e19]">{{ displayBrand }}</span
          >?
        </h2>
        <button
          type="button"
          class="inline-flex w-full items-center justify-center gap-2 rounded-[4px] bg-[#aa0e19] px-7 py-4 font-sans text-[0.9rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px sm:w-auto"
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
  font-family: ui-sans-serif, system-ui, Tahoma, "Segoe UI", sans-serif;
}

.landing-root--geist {
  font-family: "Geist Sans", geistSans, ui-sans-serif, system-ui, sans-serif;
}

.landing-hero {
  position: relative;
  background-color: #cccccc;
  min-height: min(100svh, 720px);
}

.landing-hero__bg {
  position: absolute;
  inset: 0;
  z-index: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: 50% 50%;
  pointer-events: none;
}

@media (min-width: 640px) {
  .landing-hero {
    min-height: 600px;
  }
}

.landing-hero-title {
  font-family: inherit;
  font-size: clamp(2.15rem, 9vw, 4.85rem);
  word-break: break-word;
}

.landing-online {
  font-family: inherit;
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
}

.landing-root--motion .landing-online__count {
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

.landing-root--motion .landing-card {
  animation: landing-float 5.5s ease-in-out infinite;
}
.landing-root--motion .landing-card--left {
  animation-delay: 0s;
}
.landing-root--motion .landing-card--you {
  animation-delay: 0.4s;
}
.landing-root--motion .landing-card--right {
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
  .landing-root--motion .landing-card {
    animation: none;
  }
  .landing-root--motion .landing-online__count {
    animation: none;
  }
}
</style>
