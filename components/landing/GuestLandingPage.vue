<script setup lang="ts">
import {
  computed,
  defineAsyncComponent,
  onBeforeUnmount,
  onMounted,
  ref,
} from "vue";
import { useI18n } from "vue-i18n";
import { useBranding } from "~/composables/useBranding";
import { loginLinks } from "~/utilities/loginLinks";
import PlayerSiteLevel from "~/components/PlayerSiteLevel.vue";

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

const isFa = computed(() =>
  String(locale.value || "").toLowerCase().startsWith("fa"),
);
const displayBrand = computed(() => brandName.value || "YGuard");

const CLIENT_ZIP =
  "https://github.com/kianPk/web/releases/download/yguard-client-v0.2.7/YGuardClient-0.2.7-win-x64.zip";
const AC_ZIP =
  "https://github.com/kianPk/web/releases/download/client-v0.5.2/YGuardAC-0.5.2-client.zip";

const copy = computed(() => {
  if (isFa.value) {
    return {
      challengeYour: "چالش بده",
      subtitle: `${displayBrand.value} فقط صف رنک نیست — کلاب، بتل‌پس، لول سایت هم‌تراز فیسیت، کلاینت دسکتاپ و آنتی‌چیت اختصاصی.`,
      cta: "ورود با استیم",
      ctaClient: "دانلود کلاینت",
      ctaAc: "دانلود آنتی‌چیت",
      onlineSuffix: "بازیکن آنلاین الان",
      words: ["بازی", "ایم", "مهارت", "رنک", "کلاب", "لول", "تیم"],
      level: "لول",
      featuresHeading: "چی تازه‌ای به وای گارد اومده.",
      featuresSub: `از صف Competitive تا کلاب گیم‌نت و اتواپدیت کلاینت — همه‌چیز برای رقابت تمیز CS2.`,
      features: [
        {
          title: "مچ‌میکینگ و لول سایت",
          body: "Competitive، Wingman، Duel. لول سایت با XP بازی بالا می‌رود و می‌تواند از لول فیسیت هم شروع شود.",
          cta: "ورود به صف",
          redirect: "/play",
        },
        {
          title: "کلاب‌ها",
          body: "کلاب بساز، عضو بگیر، سرور کلاب و شاپ داشته باش — هویت کامیونیتی جدا از تیم رقابتی.",
          cta: "کلاب‌ها",
          redirect: "/clubs",
        },
        {
          title: "گیم‌نت ×۲ XP",
          body: "گیم‌نت‌ها با کد لانچر سیستم‌هاشان را وصل می‌کنند؛ بازی از آن PCها XP سایت را دوبرابر می‌کند.",
          cta: "لانچر گیم‌نت",
          redirect: "/launcher/gamenet",
        },
        {
          title: "بتل‌پس و اسکین",
          body: "چلنج بزن، ریوارد بگیر و لوداوتت را با اینونتوری به سرور ببر.",
          cta: "بتل‌پس",
          redirect: "/battle-pass",
        },
        {
          title: "کلاینت دسکتاپ",
          body: "اپ ویندوز با اتواپدیت: صف، سرور، کلاب و ویس بدون باز نگه داشتن مرورگر.",
          cta: "دانلود کلاینت",
          href: CLIENT_ZIP,
        },
        {
          title: "YGuard AC",
          body: "لانچر آنتی‌چیت اجباری برای رنک — سخت‌افزار چک، اتواپدیت و گیت صف.",
          cta: "دانلود AC",
          href: AC_ZIP,
        },
      ],
      downloadsHeading: "کلاینت و آنتی‌چیت.",
      downloadsSub: "یک‌بار نصب کن؛ از این به بعد آپدیت خودکار می‌آید.",
      clientEyebrow: "ویندوز",
      clientTitle: "YGuard Client",
      clientBody: "شل دسکتاپ برای صف، سرور، کلاب، بتل‌پس و تیم‌اسپیک. نسخه ۰٫۲٫۷ با اتواپدیت.",
      clientCta: "دانلود کلاینت ویندوز",
      acEyebrow: "الزامی برای رنک",
      acTitle: "YGuard Anti-Cheat",
      acBody: "لانچر AC نسخه ۰٫۵٫۲ — برای صف رنک لازم است و خودش آپدیت می‌شود.",
      acCta: "دانلود آنتی‌چیت",
      detailsLabel: "جزئیات",
      setupLabel: "راهنما",
      seoHeading: "وای گارد چیست؟",
      seoBody: [
        `${displayBrand.value} پلتفرم رقابتی کانتر استرایک ۲ است: مچ‌میکینگ، لول سایت، کلاب، بتل‌پس، سرور پابلیک/اختصاصی، اسکرم، تورنمنت و آنتی‌چیت YGuard AC.`,
        "کلاینت دسکتاپ و AC را از همین صفحه بگیر؛ برای گیم‌نت‌ها بونوس ۲× XP با کد لانچر فعال می‌شود.",
      ],
      seoLinks: [
        { to: "/cs2", label: "مچ‌میکینگ و رنک CS2" },
        { to: "/ac", label: "آنتی‌چیت وای گارد" },
        { to: "/client", label: "کلاینت دسکتاپ" },
        { to: "/clubs", label: "کلاب‌ها" },
        { to: "/servers", label: "سرور پابلیک CS2" },
      ],
      closingTitle: "آماده چالش روی",
      closingCta: "ورود با استیم",
    };
  }

  return {
    challengeYour: "Challenge your",
    subtitle: `${displayBrand.value} is more than ranked queues — clubs, battle pass, Faceit-aligned site levels, a desktop client, and YGuard AC.`,
    cta: "Sign in with Steam",
    ctaClient: "Download client",
    ctaAc: "Download anti-cheat",
    onlineSuffix: "players online right now",
    words: ["game", "aim", "skill", "rank", "club", "level", "team"],
    level: "Level",
    featuresHeading: "What's new on YGuard.",
    featuresSub: `From Competitive queues to gamenet 2× XP and auto-updating clients — built for clean CS2.`,
    features: [
      {
        title: "Matchmaking & site level",
        body: "Competitive, Wingman, Duel. Earn site XP in-game — floors can start from your Faceit skill.",
        cta: "Enter queue",
        redirect: "/play",
      },
      {
        title: "Clubs",
        body: "Create a club, invite members, run club servers and shop — community identity beyond ranked teams.",
        cta: "Browse clubs",
        redirect: "/clubs",
      },
      {
        title: "Gamenet 2× XP",
        body: "Cafes bind PCs with a launcher code. Play from those machines and site XP doubles.",
        cta: "Gamenet launcher",
        redirect: "/launcher/gamenet",
      },
      {
        title: "Battle pass & skins",
        body: "Hit challenges, claim rewards, and take your inventory loadout into servers.",
        cta: "Battle pass",
        redirect: "/battle-pass",
      },
      {
        title: "Desktop client",
        body: "Windows shell with auto-update — queues, servers, clubs, and voice without keeping the browser open.",
        cta: "Download client",
        href: CLIENT_ZIP,
      },
      {
        title: "YGuard AC",
        body: "Mandatory anti-cheat launcher for ranked — hardware checks, auto-update, queue gate.",
        cta: "Download AC",
        href: AC_ZIP,
      },
    ],
    downloadsHeading: "Client & anti-cheat.",
    downloadsSub: "Install once. Updates ship automatically from then on.",
    clientEyebrow: "Windows",
    clientTitle: "YGuard Client",
    clientBody: "Desktop shell for play, servers, clubs, battle pass, and TeamSpeak. v0.2.7 with auto-update.",
    clientCta: "Download Windows client",
    acEyebrow: "Required for ranked",
    acTitle: "YGuard Anti-Cheat",
    acBody: "AC launcher v0.5.2 — required for ranked queues and self-updates.",
    acCta: "Download anti-cheat",
    detailsLabel: "Details",
    setupLabel: "Setup",
    seoHeading: "What is YGuard?",
    seoBody: [
      `${displayBrand.value} is a competitive Counter-Strike 2 platform: ranked matchmaking, site levels, clubs, battle pass, public/dedicated servers, scrims, tournaments, and YGuard AC.`,
      "Grab the desktop client and anti-cheat here. Gamenets unlock 2× site XP with a launcher code on each PC.",
    ],
    seoLinks: [
      { to: "/cs2", label: "CS2 matchmaking & ranked" },
      { to: "/ac", label: "YGuard Anti-Cheat" },
      { to: "/client", label: "Desktop client" },
      { to: "/clubs", label: "Clubs" },
      { to: "/servers", label: "CS2 public servers" },
    ],
    closingTitle: "Ready to challenge your game on",
    closingCta: "Sign in with Steam",
  };
});

type HeroPlayer = {
  rank: number;
  name: string;
  avatar: string | null;
  level: number;
  xp: number;
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
    const { fetchTopPlayersBySiteLevel } = await import(
      "~/utils/landingSiteLevel"
    );
    const rows = await fetchTopPlayersBySiteLevel(3);
    heroPlayers.value = rows.map(
      (row, index): HeroPlayer => ({
        rank: index + 1,
        name: row.player_name || "Player",
        avatar: row.player_custom_avatar_url || row.player_avatar_url || null,
        level: row.level,
        xp: row.xp,
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

function onFeatureClick(feature: {
  redirect?: string;
  href?: string;
}) {
  if (feature.href) {
    window.open(feature.href, "_blank", "noopener,noreferrer");
    return;
  }
  loginWithSteam(feature.redirect || "/play");
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
            class="flex w-full flex-col items-stretch gap-3 pt-1 sm:w-auto sm:items-start"
          >
            <div
              class="flex w-full flex-col gap-2.5 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center sm:gap-3"
            >
              <button
                type="button"
                class="landing-cta-primary inline-flex items-center justify-center gap-2 rounded-[4px] bg-[#aa0e19] px-5 py-3.5 font-sans text-[0.85rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px sm:px-6 sm:py-4 sm:text-[0.9rem]"
                @click="loginWithSteam('/play')"
              >
                <span
                  aria-hidden="true"
                  class="inline-block h-0 w-0 border-y-[5px] border-y-transparent border-l-[8px] border-l-[#060606]"
                ></span>
                {{ copy.cta }}
              </button>
              <a
                class="landing-cta-ghost inline-flex items-center justify-center rounded-[4px] border border-[#060606]/35 bg-transparent px-4 py-3.5 font-sans text-[0.78rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-colors hover:bg-[#060606] hover:text-[#ccc] sm:px-5 sm:py-4 sm:text-[0.82rem]"
                :href="CLIENT_ZIP"
                target="_blank"
                rel="noopener noreferrer"
              >
                {{ copy.ctaClient }}
              </a>
              <a
                class="landing-cta-ghost inline-flex items-center justify-center rounded-[4px] border border-[#060606]/35 bg-transparent px-4 py-3.5 font-sans text-[0.78rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-colors hover:bg-[#060606] hover:text-[#ccc] sm:px-5 sm:py-4 sm:text-[0.82rem]"
                :href="AC_ZIP"
                target="_blank"
                rel="noopener noreferrer"
              >
                {{ copy.ctaAc }}
              </a>
            </div>

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
              <span class="inline-flex items-center gap-1.5">
                <PlayerSiteLevel
                  :level="card.player.level"
                  :xp="card.player.xp"
                  size="md"
                />
                <span class="font-mono text-[0.55rem] uppercase tracking-[0.12em] text-white/45 sm:text-[0.65rem]">
                  {{ copy.level }}
                </span>
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
          class="mt-10 grid gap-px overflow-hidden rounded-xl bg-white/10 sm:mt-12 sm:grid-cols-2 lg:grid-cols-3"
        >
          <div
            v-for="(feature, fi) in copy.features"
            :key="feature.title"
            class="landing-feature flex flex-col gap-4 bg-[#161616] p-5 sm:p-8"
            :style="motionReady ? { animationDelay: `${fi * 60}ms` } : undefined"
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
              @click="onFeatureClick(feature)"
            >
              {{ feature.cta }}
            </button>
          </div>
        </div>
      </div>
    </section>

    <section class="border-t border-white/10 bg-[#0a0a0a] text-white">
      <div class="mx-auto max-w-7xl px-4 py-14 sm:px-6 sm:py-20 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.5rem,5vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.downloadsHeading }}
        </h2>
        <p class="mt-4 max-w-2xl text-[0.95rem] leading-relaxed text-white/55 sm:text-[1.05rem]">
          {{ copy.downloadsSub }}
        </p>
        <div class="mt-10 grid gap-4 lg:grid-cols-2 lg:gap-6">
          <article
            class="landing-download relative overflow-hidden rounded-2xl border border-white/10 bg-[linear-gradient(145deg,#1a1214_0%,#121212_55%,#0e0e0e_100%)] p-6 sm:p-8"
          >
            <div
              aria-hidden="true"
              class="pointer-events-none absolute -end-8 -top-10 h-40 w-40 rounded-full bg-[#aa0e19]/20 blur-3xl"
            />
            <p
              class="m-0 font-sans text-[0.7rem] font-black uppercase tracking-[0.16em] text-[#aa0e19]"
            >
              {{ copy.clientEyebrow }}
            </p>
            <h3
              class="mt-3 m-0 font-sans text-[1.35rem] font-black uppercase tracking-[0.04em]"
            >
              {{ copy.clientTitle }}
            </h3>
            <p class="mt-3 m-0 max-w-md text-[0.95rem] leading-relaxed text-white/55">
              {{ copy.clientBody }}
            </p>
            <a
              class="mt-6 inline-flex items-center justify-center rounded-[4px] bg-[#aa0e19] px-5 py-3.5 font-sans text-[0.8rem] font-black uppercase tracking-[0.08em] text-[#060606] transition-[filter,transform] hover:brightness-110 active:translate-y-px"
              :href="CLIENT_ZIP"
              target="_blank"
              rel="noopener noreferrer"
            >
              {{ copy.clientCta }}
            </a>
            <NuxtLink
              to="/client"
              class="ms-3 inline-flex items-center justify-center rounded-[4px] border border-white/20 px-4 py-3.5 font-sans text-[0.75rem] font-bold uppercase tracking-[0.08em] text-white/70 transition-colors hover:border-white/40 hover:text-white"
            >
              {{ copy.detailsLabel }}
            </NuxtLink>
          </article>

          <article
            class="landing-download relative overflow-hidden rounded-2xl border border-white/10 bg-[linear-gradient(145deg,#12161a_0%,#121212_55%,#0e0e0e_100%)] p-6 sm:p-8"
          >
            <div
              aria-hidden="true"
              class="pointer-events-none absolute -end-8 -top-10 h-40 w-40 rounded-full bg-emerald-400/15 blur-3xl"
            />
            <p
              class="m-0 font-sans text-[0.7rem] font-black uppercase tracking-[0.16em] text-emerald-400"
            >
              {{ copy.acEyebrow }}
            </p>
            <h3
              class="mt-3 m-0 font-sans text-[1.35rem] font-black uppercase tracking-[0.04em]"
            >
              {{ copy.acTitle }}
            </h3>
            <p class="mt-3 m-0 max-w-md text-[0.95rem] leading-relaxed text-white/55">
              {{ copy.acBody }}
            </p>
            <a
              class="mt-6 inline-flex items-center justify-center rounded-[4px] border border-emerald-400/50 px-5 py-3.5 font-sans text-[0.8rem] font-black uppercase tracking-[0.08em] text-emerald-300 transition-colors hover:bg-emerald-400 hover:text-[#060606]"
              :href="AC_ZIP"
              target="_blank"
              rel="noopener noreferrer"
            >
              {{ copy.acCta }}
            </a>
            <NuxtLink
              to="/ac"
              class="ms-3 inline-flex items-center justify-center rounded-[4px] border border-white/20 px-4 py-3.5 font-sans text-[0.75rem] font-bold uppercase tracking-[0.08em] text-white/70 transition-colors hover:border-white/40 hover:text-white"
            >
              {{ copy.setupLabel }}
            </NuxtLink>
          </article>
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

.landing-root--motion .landing-feature {
  animation: landing-feature-in 0.55s ease both;
}

.landing-root--motion .landing-download {
  animation: landing-feature-in 0.65s ease both;
}

.landing-root--motion .landing-cta-primary {
  animation: landing-cta-pulse 2.8s ease-in-out infinite;
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

@keyframes landing-feature-in {
  from {
    opacity: 0;
    transform: translateY(12px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes landing-cta-pulse {
  0%,
  100% {
    box-shadow: 0 0 0 0 rgba(170, 14, 25, 0.35);
  }
  50% {
    box-shadow: 0 0 0 10px rgba(170, 14, 25, 0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .landing-root--motion .landing-card,
  .landing-root--motion .landing-feature,
  .landing-root--motion .landing-download,
  .landing-root--motion .landing-cta-primary {
    animation: none;
  }
  .landing-root--motion .landing-online__count {
    animation: none;
  }
}
</style>
