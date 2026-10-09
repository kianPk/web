<script setup lang="ts">
import type { FleetMode } from "~/composables/usePublicServerFleet";

defineProps<{
  mode: FleetMode;
}>();

function onImgError(e: Event) {
  (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
}
</script>

<template>
  <NuxtLink
    :to="`/game-servers/${mode.key}`"
    class="group relative block aspect-[5/6] overflow-hidden rounded-xl bg-card ring-1 ring-white/5 transition-all duration-300 hover:ring-[var(--mode-accent)]"
    :style="{ '--mode-accent': mode.accent }"
  >
    <img
      :src="mode.cover"
      :alt="$t(`pages.servers.modes.${mode.key}.name`)"
      class="absolute inset-0 h-full w-full object-cover brightness-[0.55] transition-all duration-500 group-hover:scale-105 group-hover:brightness-75"
      @error="onImgError"
    />
    <div
      class="absolute inset-0 flex flex-col items-center justify-center gap-0.5 p-3 text-center"
    >
      <p
        class="text-2xl font-extrabold tracking-tight text-white drop-shadow-[0_2px_8px_rgba(0,0,0,0.6)]"
      >
        {{ $t(`pages.servers.modes.${mode.key}.name`) }}
      </p>
      <p class="text-[0.7rem] font-medium text-white/80">
        {{ $t("pages.servers.in_game", { count: mode.players }) }}
      </p>
    </div>
  </NuxtLink>
</template>
