<script setup lang="ts">
import { computed } from "vue";
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
  Search,
} from "lucide-vue-next";
import { useAuthStore } from "~/stores/AuthStore";
import PlayerDisplay from "~/components/PlayerDisplay.vue";

const route = useRoute();
const auth = useAuthStore();
const { brandName, customLogoUrl } = useBranding();
const { t } = useI18n();

type RailItem = {
  id: string;
  to: string;
  icon: unknown;
  label: string;
};

const rail = computed<RailItem[]>(() => [
  {
    id: "play",
    to: "/launcher/play",
    icon: Gamepad2,
    label: t("layouts.app_nav.navigation.play"),
  },
  {
    id: "servers",
    to: "/launcher/servers",
    icon: Server,
    label: t("layouts.app_nav.navigation.game_servers"),
  },
  {
    id: "teamspeak",
    to: "/launcher/teamspeak",
    icon: Headset,
    label: t("layouts.app_nav.navigation.teamspeak"),
  },
  {
    id: "battle-pass",
    to: "/launcher/battle-pass",
    icon: Ticket,
    label: t("layouts.app_nav.navigation.battle_pass"),
  },
  {
    id: "inventory",
    to: "/launcher/inventory",
    icon: ShoppingBag,
    label: t("layouts.launcher.nav.inventory"),
  },
  {
    id: "clubs",
    to: "/launcher/clubs",
    icon: Building2,
    label: t("layouts.app_nav.navigation.clubs"),
  },
  {
    id: "scrims",
    to: "/launcher/scrims",
    icon: Swords,
    label: t("layouts.app_nav.navigation.scrims"),
  },
]);

const activeId = computed(() => {
  const path = route.path.replace(/\/$/, "") || "/launcher";
  const hit = rail.value.find(
    (item) => path === item.to || path.startsWith(`${item.to}/`),
  );
  return hit?.id || "play";
});

const title = computed(() => brandName.value || "YGuard");
</script>

<template>
  <div
    class="flex h-[100dvh] flex-col overflow-hidden bg-[#0c0d10] text-[#e8eaed] select-none"
    style="font-family: Inter, Segoe UI, system-ui, sans-serif"
  >
    <!-- Faceit-style title bar -->
    <header
      class="flex h-9 shrink-0 items-center gap-3 border-b border-white/[0.06] bg-[#12141a] px-3"
    >
      <div class="flex min-w-0 items-center gap-2">
        <NuxtImg
          class="h-5 w-5 rounded-sm"
          :src="customLogoUrl || '/favicon/64.png'"
          :alt="title"
        />
        <span class="truncate text-[11px] font-semibold tracking-wide text-white/90">
          {{ title }}
        </span>
        <span class="text-[10px] uppercase tracking-[0.18em] text-white/35">
          Client
        </span>
      </div>

      <div class="mx-auto hidden max-w-md flex-1 items-center md:flex">
        <label
          class="flex h-7 w-full items-center gap-2 rounded-md bg-white/[0.04] px-2.5 ring-1 ring-white/[0.06]"
        >
          <Search class="h-3.5 w-3.5 text-white/35" />
          <input
            type="search"
            readonly
            :placeholder="t('layouts.launcher.search_placeholder')"
            class="w-full bg-transparent text-[11px] text-white/70 outline-none placeholder:text-white/30"
          />
        </label>
      </div>

      <div class="ms-auto flex items-center gap-0.5 text-white/45">
        <button
          type="button"
          class="grid h-7 w-8 place-items-center rounded hover:bg-white/[0.06] hover:text-white/80"
          tabindex="-1"
          aria-hidden="true"
        >
          <Minus class="h-3.5 w-3.5" />
        </button>
        <button
          type="button"
          class="grid h-7 w-8 place-items-center rounded hover:bg-white/[0.06] hover:text-white/80"
          tabindex="-1"
          aria-hidden="true"
        >
          <Square class="h-3 w-3" />
        </button>
        <NuxtLink
          to="/me"
          class="grid h-7 w-8 place-items-center rounded hover:bg-[#e81123] hover:text-white"
          :title="t('layouts.launcher.open_site')"
        >
          <X class="h-3.5 w-3.5" />
        </NuxtLink>
      </div>
    </header>

    <div class="flex min-h-0 flex-1">
      <!-- Faceit-style left rail -->
      <aside
        class="flex w-[72px] shrink-0 flex-col border-e border-white/[0.06] bg-[#101218]"
      >
        <nav class="flex flex-1 flex-col gap-1 px-2 py-3">
          <NuxtLink
            v-for="item in rail"
            :key="item.id"
            :to="item.to"
            class="group relative flex flex-col items-center gap-1 rounded-md px-1 py-2.5 text-white/45 transition hover:bg-white/[0.04] hover:text-white/85"
            :class="
              activeId === item.id
                ? '!bg-white/[0.06] !text-[#ff5500]'
                : ''
            "
            :title="item.label"
          >
            <span
              v-if="activeId === item.id"
              class="absolute start-0 top-1/2 h-8 w-[3px] -translate-y-1/2 rounded-e bg-[#ff5500]"
            />
            <component :is="item.icon" class="h-5 w-5" :stroke-width="1.75" />
            <span
              class="max-w-full truncate text-center text-[9px] font-medium leading-tight tracking-wide"
            >
              {{ item.label }}
            </span>
          </NuxtLink>
        </nav>

        <div class="border-t border-white/[0.06] p-2">
          <NuxtLink
            v-if="auth.me"
            to="/launcher/play"
            class="flex flex-col items-center gap-1 rounded-md px-1 py-2 hover:bg-white/[0.04]"
          >
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
          </NuxtLink>
          <NuxtLink
            v-else
            to="/login"
            class="block rounded-md px-1 py-2 text-center text-[10px] font-semibold uppercase tracking-wide text-[#ff5500] hover:bg-white/[0.04]"
          >
            {{ t("layouts.launcher.sign_in") }}
          </NuxtLink>
        </div>
      </aside>

      <!-- Content -->
      <main class="min-w-0 flex-1 overflow-hidden bg-[#0c0d10]">
        <slot />
      </main>
    </div>
  </div>
</template>
