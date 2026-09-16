<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useBranding } from "~/composables/useBranding";
import { loginLinks } from "~/utilities/loginLinks";
import LandingRankings from "~/components/landing/LandingRankings.vue";

const { locale } = useI18n();
const { brandName, logoUrl } = useBranding();

const isFa = computed(() =>
  String(locale.value || "").toLowerCase().startsWith("fa"),
);
const displayBrand = computed(() => brandName.value || "YGuard");

const copy = computed(() => {
  if (isFa.value) {
    return {
      challengeYour: "چالش بده",
      subtitle: `بازیکن‌ها برای بهترین تجربه رقابتی کانتر به ${displayBrand.value} می‌آیند. مچ‌میکینگ، Elo و پیشرفت بدون سقف.`,
      cta: "Play now",
      onlineSuffix: "بازیکن آنلاین الان",
      words: ["بازی", "ایم", "مهارت", "رنک", "محدودیت", "ذهن", "تیم"],
      ranked: "Ranked",
      you: "You",
      peak: "Peak",
      featuresHeading: "More than matchmaking.",
      featuresSub: `${displayBrand.value} is a home for every ambition — competitive queues, live matches, inventory, and community tools.`,
      features: [
        {
          title: "Matchmaking",
          body: "Solo or stack. Grind Elo in Competitive, Wingman, or Duel.",
          cta: "Play now",
          action: "login" as const,
        },
        {
          title: "Watch",
          body: "Follow live matches, streams, and highlights as they happen.",
          cta: "Watch live",
          to: "/watch",
        },
        {
          title: "Inventory",
          body: "Build your loadout, try skins, and take your setup into servers.",
          cta: "Open inventory",
          to: "/apps/inventory",
        },
        {
          title: "Leaderboard",
          body: "Climb the rankings and see where you stand against everyone else.",
          cta: "View rankings",
          to: "/leaderboard",
        },
      ],
      closingTitle: "Ready to challenge your game on",
      closingCta: "Play now",
    };
  }

  return {
    challengeYour: "Challenge your",
    subtitle: `Players come to ${displayBrand.value} for competitive Counter-Strike at its best. Grind matchmaking, climb Elo, and prove yourself with no ceiling on how far you can go.`,
    cta: "Play now",
    onlineSuffix: "players online right now",
    words: ["game", "aim", "skill", "rank", "limits", "mind", "team"],
    ranked: "Ranked",
    you: "You",
    peak: "Peak",
    featuresHeading: "More than matchmaking.",
    featuresSub: `${displayBrand.value} is a home for every ambition — competitive queues, live matches, inventory, and community tools.`,
    features: [
      {
        title: "Matchmaking",
        body: "Solo or stack. Grind Elo in Competitive, Wingman, or Duel.",
        cta: "Play now",
        action: "login" as const,
      },
      {
        title: "Watch",
        body: "Follow live matches, streams, and highlights as they happen.",
        cta: "Watch live",
        to: "/watch",
      },
      {
        title: "Inventory",
        body: "Build your loadout, try skins, and take your setup into servers.",
        cta: "Open inventory",
        to: "/apps/inventory",
      },
      {
        title: "Leaderboard",
        body: "Climb the rankings and see where you stand against everyone else.",
        cta: "View rankings",
        to: "/leaderboard",
      },
    ],
    closingTitle: "Ready to challenge your game on",
    closingCta: "Play now",
  };
});

const wordIndex = ref(0);
const onlineCount = ref<number | null>(null);
let wordTimer: ReturnType<typeof setInterval> | undefined;
let onlineTimer: ReturnType<typeof setInterval> | undefined;

async function refreshOnlineCount() {
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const data = await $fetch<{ count: number }>(
      `https://${apiDomain}/sockets/players-online`,
    );
    onlineCount.value = Number(data?.count) || 0;
  } catch {
    // Keep last known value; Faceit-style chrome still renders.
  }
}

onMounted(() => {
  wordTimer = setInterval(() => {
    wordIndex.value = (wordIndex.value + 1) % copy.value.words.length;
  }, 2200);
  void refreshOnlineCount();
  onlineTimer = setInterval(() => {
    void refreshOnlineCount();
  }, 15000);
});

onBeforeUnmount(() => {
  if (wordTimer) clearInterval(wordTimer);
  if (onlineTimer) clearInterval(onlineTimer);
});

const activeWord = computed(
  () => copy.value.words[wordIndex.value] ?? copy.value.words[0],
);

const onlineLabel = computed(() => {
  const n = onlineCount.value;
  if (n == null) return null;
  return `${n.toLocaleString()} ${copy.value.onlineSuffix}`;
});

function loginWithSteam() {
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(window.location.origin + "/play")}`;
}
</script>

<template>
  <div class="landing-root overflow-x-hidden" :dir="isFa ? 'rtl' : 'ltr'">
    <!-- Light Faceit-style hero -->
    <section
      class="relative isolate overflow-hidden bg-[#e8e8e8] text-[#060606]"
    >
      <div
        aria-hidden="true"
        class="pointer-events-none absolute inset-0 opacity-[0.35] [background-image:repeating-linear-gradient(-18deg,transparent_0,transparent_42px,rgba(0,0,0,0.045)_42px,rgba(0,0,0,0.045)_84px)]"
      ></div>

      <div
        class="relative z-10 mx-auto grid min-h-[min(100svh,920px)] max-w-7xl items-center gap-12 px-6 py-16 lg:grid-cols-[1.05fr_0.95fr] lg:gap-8 lg:px-10 lg:py-20"
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
            class="m-0 max-w-[13ch] font-sans text-[clamp(2.75rem,7.2vw,4.85rem)] font-black uppercase leading-[0.92] tracking-[-0.03em] text-[#060606]"
          >
            <span class="block">{{ copy.challengeYour }}</span>
            <span
              class="relative mt-1 inline-block min-h-[1.05em] text-[#ff4b00]"
              :key="activeWord"
            >
              {{ activeWord }}.
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
              class="inline-flex items-center gap-2 rounded-[4px] bg-[#ff4b00] px-6 py-4 font-sans text-[0.9rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px"
              @click="loginWithSteam"
            >
              <span
                aria-hidden="true"
                class="inline-block h-0 w-0 border-y-[5px] border-y-transparent border-l-[8px] border-l-[#060606]"
              ></span>
              {{ copy.cta }}
            </button>

            <!-- Faceit-style: | ● N players online right now -->
            <div
              v-if="onlineLabel"
              class="inline-flex items-center gap-3 text-[0.95rem] leading-none text-[#2a2a2a]"
            >
              <span
                aria-hidden="true"
                class="hidden h-8 w-px bg-[#bdbdbd] sm:block"
              ></span>
              <span
                aria-hidden="true"
                class="h-2.5 w-2.5 shrink-0 rounded-full bg-[#3ddc84]"
              ></span>
              <span class="font-sans font-medium tracking-[0.01em]">
                {{ onlineLabel }}
              </span>
            </div>
          </div>
        </div>

        <div
          class="relative mx-auto flex w-full max-w-md items-end justify-center gap-3 sm:max-w-lg lg:max-w-none lg:justify-end"
          aria-hidden="true"
        >
          <div
            class="landing-card landing-card--left flex h-[280px] w-[32%] max-w-[160px] flex-col items-center justify-center gap-3 rounded-2xl bg-[#1a2332] sm:h-[340px]"
          >
            <div
              class="flex h-16 w-16 items-center justify-center rounded-full bg-white/10 sm:h-20 sm:w-20"
            >
              <span class="text-2xl font-black text-[#ff4b00]">1</span>
            </div>
            <span
              class="px-2 text-center font-sans text-[0.7rem] font-bold uppercase tracking-[0.12em] text-white/80"
              >{{ copy.ranked }}</span
            >
          </div>

          <div
            class="landing-card landing-card--you relative z-10 flex h-[320px] w-[38%] max-w-[190px] flex-col items-center justify-center gap-4 rounded-2xl border border-[#ff4b00]/35 bg-[#1a1a1a] shadow-[0_18px_40px_rgba(0,0,0,0.28)] sm:h-[400px]"
          >
            <div
              class="flex h-20 w-20 items-center justify-center rounded-full border-2 border-[#ff4b00]/55 bg-[#121212] sm:h-24 sm:w-24"
            >
              <img
                v-if="logoUrl"
                :src="logoUrl"
                alt=""
                class="h-12 w-12 object-contain sm:h-14 sm:w-14"
              />
              <NuxtImg
                v-else
                src="/favicon/64.png"
                alt=""
                class="h-12 w-12 object-contain sm:h-14 sm:w-14"
              />
            </div>
            <span
              class="font-sans text-[0.85rem] font-black uppercase tracking-[0.16em] text-white"
              >{{ copy.you }}</span
            >
          </div>

          <div
            class="landing-card landing-card--right flex h-[280px] w-[32%] max-w-[160px] flex-col items-center justify-center gap-3 rounded-2xl bg-[#1f1a12] sm:h-[340px]"
          >
            <div
              class="flex h-16 w-16 items-center justify-center rounded-full bg-[#ff4b00]/15 sm:h-20 sm:w-20"
            >
              <span class="text-2xl font-black text-[#ff4b00]">★</span>
            </div>
            <span
              class="px-2 text-center font-sans text-[0.7rem] font-bold uppercase tracking-[0.12em] text-white/80"
              >{{ copy.peak }}</span
            >
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
              v-if="feature.action === 'login'"
              type="button"
              class="inline-flex w-fit items-center gap-2 rounded-[4px] border border-[#ff4b00] px-4 py-2.5 font-sans text-[0.72rem] font-black uppercase tracking-[0.12em] text-[#ff4b00] transition-colors hover:bg-[#ff4b00] hover:text-[#060606]"
              @click="loginWithSteam"
            >
              {{ feature.cta }}
            </button>
            <NuxtLink
              v-else
              :to="feature.to"
              class="inline-flex w-fit items-center gap-2 rounded-[4px] border border-[#ff4b00] px-4 py-2.5 font-sans text-[0.72rem] font-black uppercase tracking-[0.12em] text-[#ff4b00] no-underline transition-colors hover:bg-[#ff4b00] hover:text-[#060606]"
            >
              {{ feature.cta }}
            </NuxtLink>
          </div>
        </div>
      </div>
    </section>

    <LandingRankings />

    <section class="border-t border-white/10 bg-[#121212] text-white">
      <div
        class="mx-auto flex max-w-7xl flex-col items-start gap-6 px-6 py-20 lg:flex-row lg:items-center lg:justify-between lg:px-10"
      >
        <h2
          class="m-0 max-w-xl font-sans text-[clamp(1.75rem,4vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em]"
        >
          {{ copy.closingTitle }}
          <span class="text-[#ff4b00]">{{ displayBrand }}</span
          >?
        </h2>
        <button
          type="button"
          class="inline-flex items-center gap-2 rounded-[4px] bg-[#ff4b00] px-7 py-4 font-sans text-[0.9rem] font-black uppercase tracking-[0.06em] text-[#060606] transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px"
          @click="loginWithSteam"
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
}
</style>
