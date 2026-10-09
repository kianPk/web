<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Gamepad2, Radio, Settings2, Users } from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";
import { useAuthStore } from "~/stores/AuthStore";
import { e_player_roles_enum } from "~/generated/zeus";

const { loading, servers, modes, totalPlayers } = usePublicServerFleet();

const canManage = computed(() =>
  useAuthStore().isRoleAbove(e_player_roles_enum.moderator),
);

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
    <section
      class="relative overflow-hidden rounded-2xl bg-[linear-gradient(115deg,hsl(160_45%_22%)_0%,hsl(200_30%_14%)_45%,hsl(var(--card))_100%)] p-6 ring-1 ring-white/5 md:p-8"
    >
      <div
        class="pointer-events-none absolute -end-24 -top-24 h-72 w-72 rounded-full bg-[hsl(var(--tac-amber)/0.18)] blur-3xl"
      />
      <div class="relative flex flex-wrap items-start justify-between gap-4">
        <div class="max-w-2xl space-y-2">
          <h1 class="text-2xl font-extrabold tracking-tight md:text-3xl">
            {{ $t("pages.servers.hero_title") }}
          </h1>
          <p class="text-sm leading-relaxed text-white/75">
            {{ $t("pages.servers.subtitle") }}
          </p>
        </div>
        <Button v-if="canManage" as-child variant="outline" size="sm">
          <NuxtLink to="/dedicated-servers/create">
            <Settings2 class="h-4 w-4" />
            <span class="hidden md:inline">{{
              $t("pages.public_servers.setup_public_server")
            }}</span>
          </NuxtLink>
        </Button>
      </div>

      <div class="relative mt-6 flex flex-wrap gap-2">
        <div
          v-for="stat in stats"
          :key="stat.key"
          class="flex items-center gap-3 rounded-lg bg-black/25 px-4 py-2.5 backdrop-blur-sm"
        >
          <component
            :is="stat.icon"
            class="h-5 w-5 text-[hsl(var(--tac-amber))]"
          />
          <div class="leading-tight">
            <p class="text-[0.7rem] text-white/60">{{ $t(stat.label) }}</p>
            <p class="font-mono text-sm font-bold tabular-nums">
              {{ stat.value }}
            </p>
          </div>
        </div>
      </div>
    </section>
  </PageTransition>

  <PageTransition :delay="80">
    <div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
      <template v-if="!ready">
        <Skeleton v-for="i in 3" :key="i" class="aspect-[5/6] rounded-xl" />
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
