<script setup lang="ts">
import { onMounted, ref } from "vue";

// The Servers section's banner: a looping gameplay clip, shown whole (the box
// keeps the clip's 1280x714 shape), with the text over its darkened bottom.
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
    class="relative flex aspect-[1280/714] flex-col overflow-hidden rounded-xl bg-black ring-1 ring-white/5"
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
      class="absolute inset-0 bg-gradient-to-r from-black/60 via-black/20 to-transparent rtl:bg-gradient-to-l"
    />
    <div
      class="absolute inset-x-0 bottom-0 h-3/5 bg-gradient-to-t from-black/85 via-black/40 to-transparent"
    />
    <div class="relative flex flex-1 flex-col">
      <slot />
    </div>
  </section>
</template>
