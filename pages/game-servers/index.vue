<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Gamepad2, Radio, Users } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import ServersHero from "~/components/servers/ServersHero.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const { loading, servers, modes, totalPlayers } = usePublicServerFleet();

const ready = computed(() => !loading.value || servers.value.length > 0);

const heroSlides = computed(() =>
  modes.value.map((mode) => ({ src: mode.cover, accent: mode.accent })),
);

const stats = computed(() => [
  {
    key: "players",
    icon: Users,
    label: "pages.servers.stat_players",
    value: totalPlayers.value,
  },
  {
    key: "servers",
    icon: Radio,
    label: "pages.servers.stat_servers",
    value: servers.value.length,
  },
  {
    key: "modes",
    icon: Gamepad2,
    label: "pages.servers.stat_modes",
    value: modes.value.length,
  },
]);

const { t } = useI18n();
useHead(() => ({ title: t("pages.servers.title") }));
</script>

<template>
  <PageTransition :delay="0">
    <ServersHero :slides="heroSlides">
      <div class="flex flex-1 flex-col justify-end p-6 md:p-10">
        <div class="max-w-xl space-y-3">
          <span
            class="inline-flex items-center gap-2 rounded-full bg-white/[0.06] px-3 py-1 text-[0.7rem] font-medium text-white/80 ring-1 ring-white/10 backdrop-blur-sm"
          >
            <span class="relative flex h-2 w-2">
              <span
                class="absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75 motion-safe:animate-ping"
              />
              <span class="relative inline-flex h-2 w-2 rounded-full bg-emerald-400" />
            </span>
            {{ $t("pages.servers.stat_players") }}:
            <b class="tabular-nums text-white">{{ totalPlayers }}</b>
          </span>
          <h1 class="text-3xl font-extrabold tracking-tight md:text-5xl">
            {{ $t("pages.servers.hero_title") }}
          </h1>
          <p class="text-sm leading-relaxed text-white/75 md:text-base">
            {{ $t("pages.servers.subtitle") }}
          </p>
        </div>

        <div class="mt-6 flex flex-wrap gap-2">
          <div
            v-for="stat in stats"
            :key="stat.key"
            class="flex items-center gap-3 rounded-xl bg-white/[0.06] px-3.5 py-2.5 ring-1 ring-white/10 backdrop-blur-md"
          >
            <span
              class="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-[#e3d39a]/10"
            >
              <component :is="stat.icon" class="h-4 w-4 text-[#e3d39a]" />
            </span>
            <div class="leading-tight">
              <p class="text-[0.65rem] text-white/60">{{ $t(stat.label) }}</p>
              <p class="text-base font-bold tabular-nums">
                {{ stat.value }}
              </p>
            </div>
          </div>
        </div>
      </div>
    </ServersHero>
  </PageTransition>

  <PageTransition :delay="80">
    <div
      class="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
    >
      <template v-if="!ready">
        <Skeleton v-for="i in 3" :key="i" class="aspect-[4/5] rounded-lg" />
      </template>
      <ServerModeTile
        v-for="(mode, index) in modes"
        v-else
        :key="mode.key"
        :mode="mode"
        class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
        :style="{ animationDelay: `${index * 50}ms` }"
      />
    </div>
  </PageTransition>
</template>
