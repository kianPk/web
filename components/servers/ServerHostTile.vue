<script setup lang="ts">
import { Server } from "lucide-vue-next";

withDefaults(
  defineProps<{
    navigate?: boolean;
  }>(),
  { navigate: true },
);
</script>

<template>
  <component
    :is="navigate ? 'NuxtLink' : 'div'"
    :to="navigate ? '/game-servers/host' : undefined"
    class="host-tile group relative block aspect-[4/5] overflow-hidden rounded-lg ring-1 ring-[hsl(var(--tac-amber)/0.35)] transition duration-300 hover:ring-[hsl(var(--tac-amber)/0.7)] hover:shadow-[0_0_28px_hsla(var(--tac-amber)/0.18)]"
    :role="navigate ? undefined : 'presentation'"
  >
    <div class="host-tile-bg absolute inset-0" aria-hidden="true" />
    <div
      class="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-transparent via-[hsl(var(--tac-amber))] to-transparent opacity-80"
      aria-hidden="true"
    />

    <div
      class="absolute inset-0 flex flex-col items-center justify-center gap-2 p-3 text-center"
    >
      <span
        class="inline-flex items-center gap-1 rounded-md border border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.12)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.18em] text-[hsl(var(--tac-amber))]"
      >
        <Server class="h-3 w-3" />
        {{ $t("pages.servers.host.name") }}
      </span>
      <p
        class="text-xl font-bold tracking-tight text-white transition-transform duration-300 group-hover:-translate-y-0.5 md:text-2xl"
      >
        {{ $t("pages.servers.host.tagline") }}
      </p>
      <p class="max-w-[12rem] text-[0.7rem] font-light leading-snug text-white/55">
        {{ $t("pages.servers.host.subtitle") }}
      </p>
      <span
        class="mt-1 text-[11px] font-semibold text-[hsl(var(--tac-amber))] opacity-0 transition-opacity duration-300 group-hover:opacity-100"
      >
        {{ $t("pages.servers.host.rent") }} →
      </span>
    </div>
  </component>
</template>

<style scoped>
.host-tile-bg {
  background:
    radial-gradient(
      ellipse 80% 55% at 50% 15%,
      hsla(38, 85%, 48%, 0.28),
      transparent 60%
    ),
    repeating-linear-gradient(
      -18deg,
      transparent,
      transparent 11px,
      rgba(255, 255, 255, 0.02) 11px,
      rgba(255, 255, 255, 0.02) 12px
    ),
    linear-gradient(165deg, #1a140c 0%, #0a0c10 55%, #12151c 100%);
}
</style>
