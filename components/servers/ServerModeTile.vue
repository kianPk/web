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
    class="group relative block aspect-[4/5] overflow-hidden rounded-lg bg-card ring-1 ring-white/5 transition-shadow duration-300 hover:ring-white/20"
  >
    <img
      :src="mode.cover"
      :alt="$t(`pages.servers.modes.${mode.key}.name`)"
      class="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
      @error="onImgError"
    />
    <div
      class="absolute inset-0 bg-black/45 transition-colors duration-300 group-hover:bg-black/75"
    />
    <div
      class="absolute inset-0 flex flex-col items-center justify-center p-3 text-center"
    >
      <p
        class="text-xl font-bold tracking-tight text-white drop-shadow-[0_2px_8px_rgba(0,0,0,0.6)] transition-transform duration-300 group-hover:-translate-y-1 md:text-2xl"
      >
        {{ $t(`pages.servers.modes.${mode.key}.name`) }}
      </p>
      <p class="text-[0.7rem] font-light text-[#cbd2d9]">
        {{ $t("pages.servers.in_game", { count: mode.players }) }}
      </p>
      <p
        class="mt-0 max-h-0 overflow-hidden text-[0.7rem] font-light leading-snug text-white opacity-0 transition-all duration-300 group-hover:mt-2 group-hover:max-h-20 group-hover:opacity-100"
      >
        {{ $t(`pages.servers.modes.${mode.key}.subtitle`) }}
      </p>
    </div>
  </NuxtLink>
</template>
