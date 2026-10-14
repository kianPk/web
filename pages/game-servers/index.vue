<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Gamepad2, Palette, Users, Zap } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const { loading, servers, modes, totalPlayers } = usePublicServerFleet();

const ready = computed(() => !loading.value || servers.value.length > 0);

const { t } = useI18n();

const modeNames = computed(() =>
  modes.value
    .map((mode) => t(`pages.servers.modes.${mode.key}.name`))
    .join(t("pages.servers.highlights.list_separator")),
);

const highlights = computed(() => [
  {
    key: "live",
    icon: Users,
    accent: "#46a758",
    live: true,
    title: t("pages.servers.highlights.live.title", {
      count: totalPlayers.value,
    }),
    text: t("pages.servers.highlights.live.text", {
      count: servers.value.length,
    }),
  },
  {
    key: "modes",
    icon: Gamepad2,
    accent: "#f5a524",
    live: false,
    title: t("pages.servers.highlights.modes.title", {
      count: modes.value.length,
    }),
    text: t("pages.servers.highlights.modes.text", { list: modeNames.value }),
  },
  {
    key: "quick",
    icon: Zap,
    accent: "#3e8ed0",
    live: false,
    title: t("pages.servers.highlights.quick.title"),
    text: t("pages.servers.highlights.quick.text"),
  },
  {
    key: "skins",
    icon: Palette,
    accent: "#8e4ec6",
    live: false,
    title: t("pages.servers.highlights.skins.title"),
    text: t("pages.servers.highlights.skins.text"),
  },
]);

useHead(() => ({ title: t("pages.servers.title") }));
</script>

<template>
  <PageTransition :delay="0">
    <h1 class="text-2xl font-extrabold tracking-tight md:text-3xl">
      {{ $t("pages.servers.hero_title") }}
    </h1>

    <div class="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-4">
      <div
        v-for="(item, index) in highlights"
        :key="item.key"
        class="relative overflow-hidden rounded-xl bg-card p-4 ring-1 ring-white/5 animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
        :style="{
          animationDelay: `${index * 60}ms`,
          backgroundImage: `radial-gradient(120% 140% at 0% 0%, ${item.accent}26 0%, transparent 60%)`,
        }"
      >
        <div class="flex items-start gap-3">
          <span
            class="relative inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg"
            :style="{ backgroundColor: `${item.accent}1f` }"
          >
            <component
              :is="item.icon"
              class="h-5 w-5"
              :style="{ color: item.accent }"
            />
            <span
              v-if="item.live"
              class="absolute -right-0.5 -top-0.5 flex h-2.5 w-2.5"
            >
              <span
                class="absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75 motion-safe:animate-ping"
              />
              <span
                class="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-400"
              />
            </span>
          </span>
          <div class="min-w-0 space-y-1">
            <p class="text-base font-bold leading-tight tabular-nums">
              {{ item.title }}
            </p>
            <p class="text-xs leading-relaxed text-muted-foreground">
              {{ item.text }}
            </p>
          </div>
        </div>
      </div>
    </div>
  </PageTransition>

  <PageTransition :delay="80">
    <div
      class="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
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
