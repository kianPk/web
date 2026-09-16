<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useBranding } from "~/composables/useBranding";
import { useMatchmakingStore } from "~/stores/MatchmakingStore";
import { loginLinks } from "~/utilities/loginLinks";
import SteamIcon from "~/components/icons/SteamIcon.vue";

const { locale } = useI18n();
const { brandName, logoUrl } = useBranding();
const matchmakingStore = useMatchmakingStore();

const isFa = computed(() => String(locale.value || "").toLowerCase().startsWith("fa"));
const displayBrand = computed(() => brandName.value || "YGuard");

const copy = computed(() => {
  if (isFa.value) {
    return {
      challengeYour: "چالش بده",
      subtitle: `بازیکن‌ها برای بهترین تجربه رقابتی کانتر به ${displayBrand.value} می‌آیند. مچ‌میکینگ، Elo و پیشرفت بدون سقف.`,
      cta: "همین حالا بازی کن",
      online: (count: string) => `${count} بازیکن آنلاین الان`,
      words: ["بازی", "ایم", "مهارت", "رنک", "محدودیت", "ذهن", "تیم"],
      ranked: "رنکد",
      you: "تو",
      peak: "پیک",
      featuresHeading: "فراتر از مچ‌میکینگ.",
      featuresSub: `${displayBrand.value} صف رقابتی، مچ زنده، اینونتوری و ابزارهای کامیونیتی را یک‌جا دارد.`,
      features: [
        {
          title: "مچ‌میکینگ",
          body: "وارد Competitive، Wingman یا Duel شو؛ مچ‌میکینگ بر پایه Elo برای CS2 جدی.",
          cta: "بازی کن",
          action: "login" as const,
        },
        {
          title: "تماشا",
          body: "مچ‌های زنده، استریم و هایلایت‌های کامیونیتی را دنبال کن.",
          cta: "تماشای زنده",
          to: "/watch",
        },
        {
          title: "اینونتوری",
          body: "لوداوت بساز، اسکین امتحان کن و ستاپ را به سرورهای کاستوم ببر.",
          cta: "باز کردن اینونتوری",
          to: "/apps/inventory",
        },
        {
          title: "لیدربورد",
          body: "در رتبه‌بندی بالا برو، فرم را ببین و جایگاهت را بسنج.",
          cta: "مشاهده رنک‌ها",
          to: "/leaderboard",
        },
      ],
      closingTitle: "آماده‌ای بازی‌ات را چالش بدهی در",
      closingCta: "شروع کن",
    };
  }

  return {
    challengeYour: "Challenge your",
    subtitle: `Players come to ${displayBrand.value} for competitive Counter-Strike at its best. Grind matchmaking, climb Elo, and prove yourself with no ceiling on how far you can go.`,
    cta: "Play now",
    online: (count: string) => `${count} players online right now`,
    words: ["game", "aim", "skill", "rank", "limits", "mind", "team"],
    ranked: "Ranked",
    you: "You",
    peak: "Peak",
    featuresHeading: "More than matchmaking.",
    featuresSub: `${displayBrand.value} gives you competitive queues, live matches, inventory, and community tools in one place.`,
    features: [
      {
        title: "Matchmaking",
        body: "Jump into Competitive, Wingman, or Duel with Elo-based matchmaking built for serious CS2.",
        cta: "Play now",
        action: "login" as const,
      },
      {
        title: "Watch",
        body: "Follow live matches, streams, and highlights from the community as they happen.",
        cta: "Watch live",
        to: "/watch",
      },
      {
        title: "Inventory",
        body: "Build your loadout, try skins, and take your setup into custom servers.",
        cta: "Open inventory",
        to: "/apps/inventory",
      },
      {
        title: "Leaderboard",
        body: "Climb the rankings, track form, and see where you stand against everyone else.",
        cta: "View rankings",
        to: "/leaderboard",
      },
    ],
    closingTitle: "Ready to challenge your game on",
    closingCta: "Get started",
  };
});

const wordIndex = ref(0);
let wordTimer: ReturnType<typeof setInterval> | undefined;

onMounted(() => {
  wordTimer = setInterval(() => {
    wordIndex.value = (wordIndex.value + 1) % copy.value.words.length;
  }, 2200);
});

onBeforeUnmount(() => {
  if (wordTimer) clearInterval(wordTimer);
});

const activeWord = computed(
  () => copy.value.words[wordIndex.value] ?? copy.value.words[0],
);

const onlineCount = computed(() => {
  const n = matchmakingStore.onlinePlayerSteamIds?.length ?? 0;
  return n > 0 ? n.toLocaleString() : null;
});

function loginWithSteam() {
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(window.location.origin + "/play")}`;
}
</script>

<template>
  <div class="landing-root relative overflow-x-hidden" :dir="isFa ? 'rtl' : 'ltr'">
    <div
      aria-hidden="true"
      class="pointer-events-none absolute inset-0 bg-[#121212]"
    ></div>
    <div
      aria-hidden="true"
      class="pointer-events-none absolute inset-0 opacity-[0.55] [background:radial-gradient(ellipse_at_70%_20%,rgba(255,75,0,0.22),transparent_55%),radial-gradient(ellipse_at_10%_80%,rgba(255,75,0,0.08),transparent_45%)]"
    ></div>
    <div
      aria-hidden="true"
      class="pointer-events-none absolute inset-0 opacity-[0.07] [background-image:repeating-linear-gradient(-18deg,transparent_0,transparent_18px,#fff_18px,#fff_19px)]"
    ></div>

    <section
      class="relative z-10 mx-auto grid min-h-svh max-w-7xl items-center gap-12 px-6 py-16 lg:grid-cols-[1.05fr_0.95fr] lg:gap-8 lg:px-10 lg:py-20"
    >
      <div class="flex flex-col items-start gap-7">
        <div class="flex items-center gap-3">
          <img
            v-if="logoUrl"
            :src="logoUrl"
            :alt="displayBrand"
            class="h-10 w-10 object-contain"
          />
          <NuxtImg
            v-else
            src="/favicon/64.png"
            :alt="displayBrand"
            class="h-10 w-10 object-contain"
          />
          <span
            class="font-sans text-[1.35rem] font-black uppercase tracking-[0.14em] text-white"
          >
            {{ displayBrand }}
          </span>
        </div>

        <h1
          class="m-0 max-w-[14ch] font-sans text-[clamp(2.6rem,7vw,4.75rem)] font-black uppercase leading-[0.92] tracking-[-0.02em] text-white"
        >
          <span class="block">{{ copy.challengeYour }}</span>
          <span
            class="relative mt-1 inline-block min-h-[1.05em] text-[#ff4b00] transition-opacity duration-300"
            :key="activeWord"
          >
            {{ activeWord }}.
          </span>
        </h1>

        <p
          class="m-0 max-w-xl text-[1.05rem] leading-relaxed text-white/65 sm:text-[1.125rem]"
        >
          {{ copy.subtitle }}
        </p>

        <div class="flex flex-wrap items-center gap-4 pt-1">
          <button
            type="button"
            class="inline-flex items-center gap-2.5 rounded-md bg-[#ff4b00] px-6 py-3.5 font-sans text-[0.85rem] font-black uppercase tracking-[0.14em] text-white transition-[transform,filter] duration-150 hover:brightness-110 active:translate-y-px"
            @click="loginWithSteam"
          >
            <SteamIcon class="h-4 w-4 fill-white" />
            {{ copy.cta }}
          </button>

          <div
            v-if="onlineCount"
            class="inline-flex items-center gap-2 font-sans text-[0.78rem] font-semibold uppercase tracking-[0.08em] text-white/55"
          >
            <span class="h-2 w-2 animate-pulse rounded-full bg-[#3ddc84]"></span>
            {{ copy.online(onlineCount) }}
          </div>
        </div>
      </div>

      <div
        class="relative mx-auto flex w-full max-w-md items-end justify-center gap-3 sm:max-w-lg lg:max-w-none lg:justify-end"
        aria-hidden="true"
      >
        <div
          class="landing-card landing-card--left flex h-[280px] w-[32%] max-w-[160px] flex-col items-center justify-center gap-3 rounded-2xl border border-white/10 bg-[#1a2332] sm:h-[340px]"
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
          class="landing-card landing-card--you relative z-10 flex h-[320px] w-[38%] max-w-[190px] flex-col items-center justify-center gap-4 rounded-2xl border border-[#ff4b00]/40 bg-[#1a1a1a] shadow-[0_0_40px_rgba(255,75,0,0.25)] sm:h-[400px]"
        >
          <div
            class="flex h-20 w-20 items-center justify-center rounded-full border-2 border-[#ff4b00]/60 bg-[#121212] sm:h-24 sm:w-24"
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
          class="landing-card landing-card--right flex h-[280px] w-[32%] max-w-[160px] flex-col items-center justify-center gap-3 rounded-2xl border border-white/10 bg-[#1f1a12] sm:h-[340px]"
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
    </section>

    <section class="relative z-10 border-t border-white/10 bg-[#0e0e0e]">
      <div class="mx-auto max-w-7xl px-6 py-20 lg:px-10">
        <h2
          class="m-0 max-w-3xl font-sans text-[clamp(1.75rem,4vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em] text-white"
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
              class="m-0 font-sans text-[1.05rem] font-black uppercase tracking-[0.08em] text-white"
            >
              {{ feature.title }}
            </h3>
            <p class="m-0 flex-1 text-[0.95rem] leading-relaxed text-white/55">
              {{ feature.body }}
            </p>
            <button
              v-if="feature.action === 'login'"
              type="button"
              class="inline-flex w-fit items-center gap-2 font-sans text-[0.75rem] font-black uppercase tracking-[0.14em] text-[#ff4b00] transition-colors hover:text-white"
              @click="loginWithSteam"
            >
              {{ feature.cta }}
              <span aria-hidden="true">→</span>
            </button>
            <NuxtLink
              v-else
              :to="feature.to"
              class="inline-flex w-fit items-center gap-2 font-sans text-[0.75rem] font-black uppercase tracking-[0.14em] text-[#ff4b00] no-underline transition-colors hover:text-white"
            >
              {{ feature.cta }}
              <span aria-hidden="true">→</span>
            </NuxtLink>
          </div>
        </div>
      </div>
    </section>

    <section class="relative z-10 border-t border-white/10">
      <div
        class="mx-auto flex max-w-7xl flex-col items-start gap-6 px-6 py-20 lg:flex-row lg:items-center lg:justify-between lg:px-10"
      >
        <h2
          class="m-0 max-w-xl font-sans text-[clamp(1.75rem,4vw,2.75rem)] font-black uppercase leading-[1.05] tracking-[-0.02em] text-white"
        >
          {{ copy.closingTitle }}
          <span class="text-[#ff4b00]">{{ displayBrand }}</span
          >?
        </h2>
        <button
          type="button"
          class="inline-flex items-center gap-2.5 rounded-md bg-[#ff4b00] px-7 py-4 font-sans text-[0.85rem] font-black uppercase tracking-[0.14em] text-white transition-[transform,filter] duration-150 hover:brightness-110 active:translate-y-px"
          @click="loginWithSteam"
        >
          <SteamIcon class="h-4 w-4 fill-white" />
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
