<script setup lang="ts">
import { onMounted, ref } from "vue";

// The Servers section's banner: a looping gameplay clip under a veil dark
// enough for the text over it.
const video = ref<HTMLVideoElement>();

onMounted(() => {
  const el = video.value;
  if (!el) return;
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    el.pause();
    return;
  }
  // SSR leaves `muted` as an attribute only, and an unmuted video may not
  // autoplay.
  el.muted = true;
  void el.play().catch(() => {});
});
</script>

<template>
  <section
    class="relative overflow-hidden rounded-xl bg-black ring-1 ring-white/5"
  >
    <video
      ref="video"
      class="absolute inset-0 h-full w-full object-cover"
      src="/videos/servers-hero.mp4"
      autoplay
      muted
      loop
      playsinline
      preload="auto"
      aria-hidden="true"
    />
    <div
      class="absolute inset-0 bg-gradient-to-r from-black/85 via-black/55 to-black/15 rtl:bg-gradient-to-l"
    />
    <div
      class="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-black/50 to-transparent"
    />
    <div class="relative">
      <slot />
    </div>
  </section>
</template>
