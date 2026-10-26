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
  // Desktop: X minimizes to tray (native host). Browser: leave launcher.
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
  // Deep links forced into the launcher shell still highlight the right rail.
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

const title = computed(() => brandName.value || "YGuard");
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

    <header
      class="launcher-glass relative z-20 flex h-11 shrink-0 items-center gap-3 border-b border-white/10 px-3 backdrop-blur-2xl"
      :class="isDesktop ? 'cursor-default' : ''"
      @pointerdown="onTitleBarPointerDown"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <img
          class="h-7 w-7 shrink-0 rounded-lg object-contain ring-1 ring-white/10"
          :src="brandLogo"
          :alt="title"
          width="28"
          height="28"
        />
        <div class="min-w-0 leading-tight">
          <div
            class="truncate text-[13px] font-semibold tracking-wide text-white"
          >
            {{ title }}
          </div>
          <div class="text-[10px] uppercase tracking-[0.2em] text-white/40">
            Client
          </div>
        </div>
      </div>

      <div class="ms-auto flex items-center gap-0.5 text-white/50">
        <button
          type="button"
          class="grid h-8 w-9 place-items-center rounded-lg transition hover:bg-white/10 hover:text-white"
          :title="t('layouts.launcher.minimize')"
          @click="onMinimize"
        >
          <Minus class="h-3.5 w-3.5" />
        </button>
        <button
          type="button"
          class="grid h-8 w-9 place-items-center rounded-lg transition hover:bg-white/10 hover:text-white"
          :title="t('layouts.launcher.maximize')"
          @click="onMaximize"
        >
          <Square class="h-3 w-3" />
        </button>
        <button
          type="button"
          class="grid h-8 w-9 place-items-center rounded-lg transition hover:bg-[#e81123] hover:text-white"
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
    </header>

    <div class="relative z-10 flex min-h-0 flex-1 flex-col md:flex-row">
      <aside
        class="launcher-glass order-2 flex shrink-0 flex-row items-stretch border-t border-white/10 backdrop-blur-2xl md:order-1 md:w-[5.25rem] md:flex-col md:border-e md:border-t-0"
      >
        <nav
          class="flex flex-1 gap-0.5 overflow-x-auto px-1.5 py-1.5 md:flex-col md:overflow-x-hidden md:overflow-y-auto md:px-2 md:py-3"
        >
          <NuxtLink
            v-for="item in rail"
            :key="item.id"
            :to="item.to"
            replace
            class="group relative flex min-w-[3.5rem] flex-col items-center justify-center gap-1 rounded-xl px-1.5 py-2 text-white/45 transition md:min-w-0 md:py-2.5"
            :class="
              activeId === item.id
                ? 'bg-white/[0.1] text-[#ff6a1f] shadow-[inset_0_0_0_1px_rgba(255,106,31,0.28)]'
                : 'hover:bg-white/[0.06] hover:text-white/85'
            "
            :title="item.label"
          >
            <span
              v-if="activeId === item.id"
              class="absolute inset-x-2 bottom-0 h-0.5 rounded-full bg-[#ff5500] md:inset-x-auto md:inset-y-auto md:start-0 md:top-1/2 md:h-7 md:w-[3px] md:-translate-y-1/2 md:rounded-e"
            />
            <component
              :is="item.icon"
              class="h-[1.15rem] w-[1.15rem] shrink-0 md:h-5 md:w-5"
              :stroke-width="1.75"
            />
            <span
              class="max-w-[4.5rem] truncate text-center text-[9px] font-medium leading-tight tracking-wide md:max-w-full"
            >
              {{ item.label }}
            </span>
          </NuxtLink>
        </nav>

        <div class="hidden shrink-0 border-t border-white/10 p-2 md:block">
          <NuxtLink
            v-if="auth.me"
            to="/launcher/play"
            class="flex flex-col items-center gap-1 rounded-xl px-1 py-2 transition hover:bg-white/[0.05]"
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
            :to="{
              path: '/login',
              query: {
                ...desktopQuery,
                redirect: '/launcher/play?desktop=1',
              },
            }"
            class="block rounded-xl px-1 py-2 text-center text-[10px] font-semibold uppercase tracking-wide text-[#ff5500] transition hover:bg-white/[0.05]"
          >
            {{ t("layouts.launcher.sign_in") }}
          </NuxtLink>
        </div>
      </aside>

      <!-- Real Nuxt pages swap here (not a hidden slot + v-show workspace). -->
      <main
        class="order-1 relative min-h-0 min-w-0 flex-1 p-2 pb-[4.25rem] md:order-2 md:p-3 md:pb-3 md:ps-2"
      >
        <div
          class="launcher-stage relative h-full min-h-0 overflow-x-hidden overflow-y-auto rounded-2xl border border-white/10 shadow-[inset_0_1px_0_rgba(255,255,255,0.05)] backdrop-blur-2xl"
        >
          <slot />
        </div>
      </main>
    </div>
  </div>
</template>

<style scoped>
.launcher-shell {
  background:
    radial-gradient(
      1200px 600px at 10% -10%,
      rgba(255, 85, 0, 0.18),
      transparent 55%
    ),
    radial-gradient(
      900px 500px at 90% 0%,
      rgba(56, 120, 255, 0.12),
      transparent 50%
    ),
    linear-gradient(180deg, #12141c 0%, #0a0b10 100%);
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
  /* Fill WebView; avoid transparent corners that look like a black void */
  border-radius: 0;
  background-color: #0a0b10;
}

.launcher-shell__glow {
  background: radial-gradient(
    circle at 50% 0%,
    rgba(255, 255, 255, 0.05),
    transparent 42%
  );
}

.launcher-glass {
  background: rgba(18, 20, 28, 0.45);
}

.launcher-stage {
  background:
    linear-gradient(
      160deg,
      rgba(255, 255, 255, 0.07) 0%,
      rgba(255, 255, 255, 0.03) 45%,
      rgba(10, 11, 16, 0.55) 100%
    );
}

@supports (backdrop-filter: blur(24px)) {
  .launcher-glass {
    background: rgba(18, 20, 28, 0.38);
  }
}
</style>

<style>
/* Shared by pages/launcher/* panels */
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
