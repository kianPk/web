<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { FleetMode } from "~/composables/usePublicServerFleet";

const props = defineProps<{
  mode: FleetMode;
}>();

const { t } = useI18n();

const name = computed(() =>
  props.mode.i18nKey
    ? t(`pages.servers.modes.${props.mode.i18nKey}.name`)
    : props.mode.name,
);
const tagline = computed(() =>
  props.mode.i18nKey
    ? t(`pages.servers.modes.${props.mode.i18nKey}.tagline`)
    : props.mode.description || "",
);

function onImgError(e: Event) {
  (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
}
</script>

<template>
  <NuxtLink
    :to="`/game-servers/${mode.key}`"
    class="group relative block aspect-[4/3] overflow-hidden rounded-xl border border-border/60 bg-card transition-all duration-300 hover:-translate-y-0.5 hover:border-[var(--mode-accent)] hover:shadow-[0_0_24px_-6px_var(--mode-accent)]"
    :style="{ '--mode-accent': mode.accent }"
  >
    <img
      :src="mode.cover"
      :alt="name"
      class="absolute inset-0 h-full w-full object-cover opacity-55 transition-all duration-700 group-hover:scale-[1.06] group-hover:opacity-70"
      @error="onImgError"
    />
    <div
      class="absolute inset-0 bg-gradient-to-t from-black via-black/60 to-black/20"
    />
    <div
      class="absolute inset-x-0 top-0 h-0.5 opacity-80"
      :style="{ background: mode.accent }"
    />

    <div
      class="absolute inset-0 flex flex-col items-center justify-center gap-1 p-3 text-center"
    >
      <p
        class="text-lg font-extrabold tracking-tight text-white drop-shadow-md sm:text-xl"
      >
        {{ name }}
      </p>
      <p
        class="inline-flex items-center gap-1.5 font-mono text-[0.65rem] font-semibold uppercase tracking-[0.12em]"
        :class="mode.players > 0 ? 'text-emerald-300' : 'text-white/55'"
      >
        <span
          class="h-1.5 w-1.5 rounded-full"
          :class="mode.players > 0 ? 'bg-emerald-400' : 'bg-white/40'"
        />
        {{ $t("pages.servers.in_game", { count: mode.players }) }}
      </p>
    </div>

    <div
      class="absolute inset-x-0 bottom-0 flex items-end justify-between gap-2 p-3"
    >
      <p
        class="line-clamp-2 text-[0.7rem] leading-snug text-white/70 opacity-0 transition-opacity duration-300 group-hover:opacity-100"
      >
        {{ tagline }}
      </p>
      <span
        class="shrink-0 rounded border border-white/15 bg-black/50 px-1.5 py-0.5 font-mono text-[0.6rem] text-white/75 backdrop-blur-md"
      >
        {{ $t("pages.servers.server_count", { count: mode.servers.length }) }}
      </span>
    </div>
  </NuxtLink>
</template>
