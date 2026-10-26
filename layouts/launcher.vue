<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import {
  Gamepad2,
  Server,
  Headset,
  Ticket,
  ShoppingBag,
  Building2,
  Swords,
  Minus,
  Square,
  X,
} from "lucide-vue-next";
import { useAuthStore } from "~/stores/AuthStore";
import PlayerDisplay from "~/components/PlayerDisplay.vue";

const route = useRoute();
const auth = useAuthStore();
const { brandName, logoUrl } = useBranding();
const { t } = useI18n();

const isDesktop = ref(false);
const brandLogo = computed(() => logoUrl.value || "/favicon/512.png");

onMounted(() => {
  if (!import.meta.client) return;
  const ua = navigator.userAgent || "";
  isDesktop.value =
    route.query.desktop === "1" ||
    route.query.desktop === "true" ||
    ua.includes("YGuardClient/") ||
    document.documentElement.dataset.yguardDesktop === "1" ||
    !!(window as Window & { yguardDesktop?: boolean }).yguardDesktop;
});

function postDesktop(cmd: "minimize" | "maximize" | "close" | "drag-start") {
  const chrome = (
    window as Window & {
      chrome?: { webview?: { postMessage: (m: string) => void } };
    }
  ).chrome;
  if (chrome?.webview?.postMessage) {
    chrome.webview.postMessage(cmd);
    return true;
  }
  return false;
}

function onTitleBarPointerDown(e: PointerEvent) {
  if (!isDesktop.value) return;
  if ((e.target as HTMLElement | null)?.closest("button,a,input,nav")) return;
  postDesktop("drag-start");
}

function onMinimize() {
  if (!postDesktop("minimize") && !isDesktop.value) return;
}

function onMaximize() {
  if (!postDesktop("maximize") && !isDesktop.value) return;
}

function onClose() {
  if (postDesktop("close")) return;
  navigateTo("/me");
}

type RailItem = {
  id: string;
  to: string | { path: string; query: Record<string, string> };
  icon: unknown;
  label: string;
};

const desktopQuery = computed(() => {
  if (route.query.desktop === "1" || route.query.desktop === "true") {
    return { desktop: "1" as const };
  }
  return {};
});

const rail = computed<RailItem[]>(() => {
  const q = desktopQuery.value;
  return [
    {
      id: "play",
      to: { path: "/launcher/play", query: q },
      icon: Gamepad2,
      label: t("layouts.launcher.nav.play"),
    },
    {
      id: "servers",
      to: { path: "/launcher/servers", query: q },
      icon: Server,
      label: t("layouts.launcher.nav.servers"),
    },
    {
      id: "teamspeak",
      to: { path: "/launcher/teamspeak", query: q },
      icon: Headset,
      label: t("layouts.launcher.nav.teamspeak"),
    },
    {
      id: "battle-pass",
      to: { path: "/launcher/battle-pass", query: q },
      icon: Ticket,
      label: t("layouts.launcher.nav.battle_pass"),
    },
    {
      id: "inventory",
      to: { path: "/launcher/inventory", query: q },
      icon: ShoppingBag,
      label: t("layouts.launcher.nav.inventory"),
    },
    {
      id: "clubs",
      to: { path: "/launcher/clubs", query: q },
      icon: Building2,
      label: t("layouts.launcher.nav.clubs"),
    },
    {
      id: "scrims",
      to: { path: "/launcher/scrims", query: q },
      icon: Swords,
      label: t("layouts.launcher.nav.scrims"),
    },
  ];
});

const activeId = computed(() => {
  const path = route.path.replace(/\/$/, "") || "/launcher";
  if (path.startsWith("/game-servers") || path.startsWith("/public-servers")) {
    return "servers";
  }
  if (path.startsWith("/clubs")) return "clubs";
  if (path.startsWith("/scrims") || path.startsWith("/teams")) return "scrims";
  if (path.startsWith("/battle-pass") || path.startsWith("/store")) {
    return "battle-pass";
  }
  if (path.startsWith("/draft-room") || path.startsWith("/play")) return "play";
  const hit = rail.value.find((item) => {
    const base = typeof item.to === "string" ? item.to : item.to.path;
    return path === base || path.startsWith(`${base}/`);
  });
  return hit?.id || "play";
});

const activeLabel = computed(
  () => rail.value.find((item) => item.id === activeId.value)?.label || "",
);

const title = computed(() => brandName.value || "YGuard");

const loginTo = computed(() => ({
  path: "/login",
  query: {
    ...desktopQuery.value,
    redirect: "/launcher/play?desktop=1",
  },
}));
</script>

<template>
  <div
    class="launcher-shell relative flex h-[100dvh] max-h-[100dvh] flex-col overflow-hidden text-[#e8eaed] select-none"
    :class="isDesktop ? 'launcher-shell--desktop' : 'launcher-shell--web'"
  >
    <div
      class="launcher-shell__glow pointer-events-none absolute inset-0"
      aria-hidden="true"
    />
    <div
      class="launcher-shell__grid pointer-events-none absolute inset-0"
      aria-hidden="true"
    />

    <header
      class="launcher-header launcher-glass relative z-20 flex h-14 shrink-0 items-center gap-3 border-b border-white/[0.08] px-3 sm:px-4"
      :class="isDesktop ? 'cursor-default' : ''"
      @pointerdown="onTitleBarPointerDown"
    >
      <div class="launcher-header__accent" aria-hidden="true" />

      <div class="flex min-w-0 items-center gap-3">
        <div class="launcher-brand-mark relative shrink-0">
          <img
            class="relative z-[1] h-8 w-8 rounded-[0.65rem] object-contain"
            :src="brandLogo"
            :alt="title"
            width="32"
            height="32"
          />
        </div>
        <div class="min-w-0 leading-none">
          <div
            class="truncate text-[15px] font-semibold tracking-[-0.02em] text-white"
          >
            {{ title }}
          </div>
          <div
            class="mt-1 text-[10px] font-medium uppercase tracking-[0.22em] text-white/35"
          >
            {{ t("layouts.launcher.client") }}
          </div>
        </div>
      </div>

      <div
        class="pointer-events-none absolute inset-x-0 hidden justify-center md:flex"
      >
        <div
          class="launcher-header__section flex max-w-[14rem] items-center gap-2 truncate rounded-full border border-white/[0.08] bg-white/[0.04] px-3.5 py-1.5 text-[12px] font-medium tracking-wide text-white/75 backdrop-blur-md"
        >
          <span
            class="h-1.5 w-1.5 shrink-0 rounded-full bg-[#ff5500] shadow-[0_0_8px_rgba(255,85,0,0.85)]"
            aria-hidden="true"
          />
          <span class="truncate">{{ activeLabel }}</span>
        </div>
      </div>

      <div class="ms-auto flex items-center gap-2">
        <NuxtLink
          v-if="auth.me"
          :to="{ path: '/launcher/play', query: desktopQuery }"
          class="launcher-account hidden items-center gap-2 rounded-full border border-white/[0.08] bg-white/[0.04] py-1 pe-3 ps-1 transition hover:border-white/15 hover:bg-white/[0.07] sm:flex"
        >
          <ClientOnly>
            <PlayerDisplay
              :player="auth.me"
              size="xs"
              compact
              :show-name="false"
              :show-flag="false"
              :show-role="false"
              :show-elo="false"
              :linkable="false"
            />
          </ClientOnly>
          <span
            class="max-w-[7rem] truncate text-[12px] font-medium text-white/80"
          >
            {{ auth.me.name || auth.me.steam_id }}
          </span>
        </NuxtLink>
        <NuxtLink
          v-else
          :to="loginTo"
          class="hidden rounded-full bg-[#ff5500] px-3.5 py-1.5 text-[11px] font-semibold uppercase tracking-[0.14em] text-white shadow-[0_0_20px_rgba(255,85,0,0.35)] transition hover:bg-[#ff6a1f] sm:inline-flex"
        >
          {{ t("layouts.launcher.sign_in") }}
        </NuxtLink>

        <div
          class="launcher-win-controls flex items-center overflow-hidden rounded-lg border border-white/[0.08] bg-black/20 text-white/55"
        >
          <button
            type="button"
            class="grid h-8 w-10 place-items-center transition hover:bg-white/10 hover:text-white"
            :title="t('layouts.launcher.minimize')"
            @click="onMinimize"
          >
            <Minus class="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            class="grid h-8 w-10 place-items-center transition hover:bg-white/10 hover:text-white"
            :title="t('layouts.launcher.maximize')"
            @click="onMaximize"
          >
            <Square class="h-3 w-3" />
          </button>
          <button
            type="button"
            class="grid h-8 w-10 place-items-center transition hover:bg-[#e81123] hover:text-white"
            :title="
              isDesktop
                ? t('layouts.launcher.close_to_tray')
                : t('layouts.launcher.open_site')
            "
            @click="onClose"
          >
            <X class="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </header>

    <div class="relative z-10 flex min-h-0 flex-1 flex-col md:flex-row">
      <aside
        class="launcher-rail launcher-glass order-2 flex shrink-0 flex-row items-stretch border-t border-white/[0.08] md:order-1 md:w-[5.75rem] md:flex-col md:border-e md:border-t-0"
      >
        <nav
          class="flex flex-1 gap-1 overflow-x-auto px-1.5 py-1.5 md:flex-col md:overflow-x-hidden md:overflow-y-auto md:gap-1 md:px-2 md:py-3"
        >
          <NuxtLink
            v-for="item in rail"
            :key="item.id"
            :to="item.to"
            replace
            class="launcher-rail__item group relative flex min-w-[3.75rem] flex-col items-center justify-center gap-1 rounded-xl px-1.5 py-2 text-white/40 transition duration-200 md:min-w-0 md:py-2.5"
            :class="
              activeId === item.id
                ? 'launcher-rail__item--active text-[#ff6a1f]'
                : 'hover:bg-white/[0.05] hover:text-white/85'
            "
            :title="item.label"
          >
            <span
              v-if="activeId === item.id"
              class="launcher-rail__pip absolute inset-x-2 bottom-0 h-0.5 rounded-full bg-[#ff5500] md:inset-x-auto md:inset-y-auto md:start-0 md:top-1/2 md:h-8 md:w-[3px] md:-translate-y-1/2 md:rounded-e"
            />
            <span
              class="launcher-rail__icon grid place-items-center rounded-lg p-1 transition duration-200"
              :class="
                activeId === item.id
                  ? 'bg-[rgba(255,85,0,0.12)] text-[#ff6a1f]'
                  : 'group-hover:bg-white/[0.04]'
              "
            >
              <component
                :is="item.icon"
                class="h-[1.15rem] w-[1.15rem] shrink-0 md:h-5 md:w-5"
                :stroke-width="1.75"
              />
            </span>
            <span
              class="max-w-[4.75rem] truncate text-center text-[9px] font-semibold leading-tight tracking-wide md:max-w-full"
            >
              {{ item.label }}
            </span>
          </NuxtLink>
        </nav>

        <div
          class="flex shrink-0 items-center justify-center border-s border-white/[0.08] px-2 md:hidden"
        >
          <NuxtLink
            v-if="auth.me"
            :to="{ path: '/launcher/play', query: desktopQuery }"
            class="rounded-xl p-1.5 transition hover:bg-white/[0.05]"
          >
            <ClientOnly>
              <PlayerDisplay
                :player="auth.me"
                size="xs"
                compact
                :show-name="false"
                :show-flag="false"
                :show-role="false"
                :show-elo="false"
                :linkable="false"
              />
            </ClientOnly>
          </NuxtLink>
          <NuxtLink
            v-else
            :to="loginTo"
            class="rounded-xl px-2 py-2 text-center text-[10px] font-semibold uppercase tracking-wide text-[#ff5500] transition hover:bg-white/[0.05]"
          >
            {{ t("layouts.launcher.sign_in") }}
          </NuxtLink>
        </div>
      </aside>

      <main
        class="order-1 relative min-h-0 min-w-0 flex-1 p-2 pb-[4.5rem] md:order-2 md:p-3 md:pb-3 md:ps-2"
      >
        <div class="launcher-stage relative h-full min-h-0 overflow-x-hidden overflow-y-auto">
          <slot />
        </div>
      </main>
    </div>
  </div>
</template>

<style scoped>
.launcher-shell {
  --launcher-accent: #ff5500;
  background:
    radial-gradient(
      1100px 520px at 8% -8%,
      rgba(255, 85, 0, 0.2),
      transparent 58%
    ),
    radial-gradient(
      900px 480px at 92% 0%,
      rgba(56, 120, 255, 0.1),
      transparent 52%
    ),
    linear-gradient(180deg, #141722 0%, #0a0b10 48%, #07080c 100%);
  font-family: "Segoe UI Variable", "Segoe UI", system-ui, sans-serif;
}

.launcher-shell--web {
  border-radius: 1.25rem;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow:
    0 0 0 1px rgba(0, 0, 0, 0.35),
    0 24px 80px rgba(0, 0, 0, 0.45);
}

.launcher-shell--desktop {
  border-radius: 0;
  background-color: #0a0b10;
}

.launcher-shell__glow {
  background: radial-gradient(
    circle at 50% -10%,
    rgba(255, 255, 255, 0.06),
    transparent 40%
  );
}

.launcher-shell__grid {
  opacity: 0.22;
  background-image:
    linear-gradient(rgba(255, 255, 255, 0.03) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.03) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: linear-gradient(180deg, rgba(0, 0, 0, 0.55), transparent 70%);
}

.launcher-glass {
  background: rgba(14, 16, 24, 0.55);
  backdrop-filter: blur(28px) saturate(1.25);
  -webkit-backdrop-filter: blur(28px) saturate(1.25);
}

.launcher-header__accent {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 1px;
  background: linear-gradient(
    90deg,
    transparent 0%,
    rgba(255, 85, 0, 0.55) 22%,
    rgba(255, 255, 255, 0.2) 50%,
    rgba(255, 85, 0, 0.45) 78%,
    transparent 100%
  );
}

.launcher-brand-mark {
  display: grid;
  place-items: center;
  border-radius: 0.75rem;
  padding: 2px;
  background: linear-gradient(
    145deg,
    rgba(255, 85, 0, 0.55),
    rgba(255, 255, 255, 0.12) 45%,
    rgba(56, 120, 255, 0.25)
  );
  box-shadow: 0 0 18px rgba(255, 85, 0, 0.22);
}

.launcher-brand-mark img {
  background: #0c0e14;
}

.launcher-header__section {
  animation: launcher-section-in 280ms ease-out;
}

.launcher-rail__item--active {
  background: rgba(255, 255, 255, 0.07);
  box-shadow: inset 0 0 0 1px rgba(255, 106, 31, 0.28);
}

.launcher-rail__pip {
  box-shadow: 0 0 10px rgba(255, 85, 0, 0.65);
}

.launcher-stage {
  border-radius: 1.15rem;
  border: 1px solid rgba(255, 255, 255, 0.09);
  background:
    linear-gradient(
      165deg,
      rgba(255, 255, 255, 0.075) 0%,
      rgba(255, 255, 255, 0.03) 40%,
      rgba(8, 9, 14, 0.62) 100%
    );
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.06),
    0 12px 40px rgba(0, 0, 0, 0.28);
  backdrop-filter: blur(22px) saturate(1.15);
  -webkit-backdrop-filter: blur(22px) saturate(1.15);
}

@keyframes launcher-section-in {
  from {
    opacity: 0;
    transform: translateY(-4px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .launcher-header__section {
    animation: none;
  }
}
</style>

<style>
.launcher-panel {
  box-sizing: border-box;
  width: 100%;
  max-width: 1400px;
  margin-inline: auto;
  padding: 1.25rem 1rem 2rem;
}

@media (min-width: 640px) {
  .launcher-panel {
    padding: 1.75rem 1.5rem 2.5rem;
  }
}

@media (min-width: 1024px) {
  .launcher-panel {
    padding: 2rem;
  }
}
</style>
