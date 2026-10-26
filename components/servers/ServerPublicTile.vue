<script setup lang="ts">
import { Radio } from "lucide-vue-next";

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
    :to="navigate ? '/public-servers' : undefined"
    class="public-tile group relative block aspect-[4/5] overflow-hidden rounded-lg ring-1 ring-[hsla(187,72%,42%,0.4)] transition duration-300 hover:ring-[hsla(187,72%,52%,0.75)] hover:shadow-[0_0_28px_hsla(187,72%,42%,0.2)]"
    :role="navigate ? undefined : 'presentation'"
  >
    <div class="public-tile-bg absolute inset-0" aria-hidden="true" />
    <div
      class="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-transparent via-[hsl(187,72%,52%)] to-transparent opacity-80"
      aria-hidden="true"
    />

    <div
      class="absolute inset-0 flex flex-col items-center justify-center gap-2 p-3 text-center"
    >
      <span
        class="inline-flex items-center gap-1 rounded-md border border-[hsla(187,72%,52%,0.45)] bg-[hsla(187,72%,42%,0.14)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.18em] text-[hsl(187,72%,62%)]"
      >
        <Radio class="h-3 w-3" />
        {{ $t("pages.servers.public.name") }}
      </span>
      <p
        class="text-xl font-bold tracking-tight text-white transition-transform duration-300 group-hover:-translate-y-0.5 md:text-2xl"
      >
        {{ $t("pages.servers.public.tagline") }}
      </p>
      <p class="max-w-[12rem] text-[0.7rem] font-light leading-snug text-white/55">
        {{ $t("pages.servers.public.subtitle") }}
      </p>
      <span
        class="mt-1 text-[11px] font-semibold text-[hsl(187,72%,62%)] opacity-0 transition-opacity duration-300 group-hover:opacity-100"
      >
        {{ $t("pages.servers.public.open") }} →
      </span>
    </div>
  </component>
</template>

<style scoped>
.public-tile-bg {
  background:
    radial-gradient(
      ellipse 80% 55% at 50% 15%,
      hsla(187, 72%, 42%, 0.28),
      transparent 60%
    ),
    repeating-linear-gradient(
      -18deg,
      transparent,
      transparent 11px,
      rgba(255, 255, 255, 0.02) 11px,
      rgba(255, 255, 255, 0.02) 12px
    ),
    linear-gradient(165deg, #0c161a 0%, #0a0c10 55%, #12151c 100%);
}
</style>
