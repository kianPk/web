<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Gamepad2, Radio, Users } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import ServersVideoHero from "~/components/servers/ServersVideoHero.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const { loading, servers, modes, totalPlayers } = usePublicServerFleet();

const ready = computed(() => !loading.value || servers.value.length > 0);

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
    <ServersVideoHero>
      <div class="flex flex-1 flex-col justify-end p-6 md:p-8">
        <div class="max-w-xl space-y-2">
          <h1 class="text-2xl font-bold tracking-tight md:text-3xl">
            {{ $t("pages.servers.hero_title") }}
          </h1>
          <p class="text-sm leading-relaxed text-white/80">
            {{ $t("pages.servers.subtitle") }}
          </p>
        </div>

        <div class="mt-6 flex flex-wrap gap-2">
          <div
            v-for="stat in stats"
            :key="stat.key"
            class="flex items-center gap-2.5 rounded-lg bg-white/[0.07] px-3 py-2 backdrop-blur-sm"
          >
            <component :is="stat.icon" class="h-4 w-4 text-[#e3d39a]" />
            <div class="leading-tight">
              <p class="text-[0.65rem] text-white/60">{{ $t(stat.label) }}</p>
              <p class="text-sm font-bold tabular-nums">
                {{ stat.value }}
              </p>
            </div>
          </div>
        </div>
      </div>
    </ServersVideoHero>
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
