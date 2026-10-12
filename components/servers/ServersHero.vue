<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";

export type ServersHeroSlide = {
  src: string;
  accent?: string;
};

// The Servers section's banner: map shots crossfading under a radar sweep,
// lit in the colour of whichever mode is on screen.
const props = withDefaults(
  defineProps<{
    slides: ServersHeroSlide[];
    accent?: string;
  }>(),
  { accent: "#e3d39a" },
);

const broken = ref(new Set<string>());
const visible = computed(() => {
  const seen = new Set<string>();
  return props.slides.filter((slide) => {
    if (!slide.src || broken.value.has(slide.src) || seen.has(slide.src)) {
      return false;
    }
    seen.add(slide.src);
    return true;
  });
});

const index = ref(0);
const current = computed(() => visible.value[index.value] ?? null);
const accent = computed(() => current.value?.accent || props.accent);

watch(
  () => visible.value.length,
  (length) => {
    if (index.value >= length) index.value = 0;
  },
);

function onError(src: string) {
  broken.value = new Set(broken.value).add(src);
}

let timer: ReturnType<typeof setInterval> | undefined;
onMounted(() => {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  timer = setInterval(() => {
    const length = visible.value.length;
    if (length > 1) index.value = (index.value + 1) % length;
  }, 6000);
});
onBeforeUnmount(() => clearInterval(timer));

const BLIPS = [
  { top: "28%", left: "62%", delay: "0s" },
  { top: "64%", left: "30%", delay: "1.4s" },
  { top: "40%", left: "22%", delay: "2.6s" },
];
</script>

<template>
  <section
    class="servers-hero relative isolate flex min-h-[300px] flex-col overflow-hidden rounded-xl bg-[#0a0c0f] ring-1 ring-white/5 md:min-h-[360px]"
    :style="{ '--hero-accent': accent }"
  >
    <img
      v-for="(slide, i) in visible"
      :key="slide.src"
      :src="slide.src"
      alt=""
      aria-hidden="true"
      class="hero-slide absolute inset-0 h-full w-full object-cover transition-opacity duration-[1600ms] ease-out"
      :class="i === index ? 'opacity-45' : 'opacity-0'"
      @error="onError(slide.src)"
    />

    <div class="hero-wash absolute inset-0" aria-hidden="true" />
    <div class="hero-grid absolute inset-0" aria-hidden="true" />

    <div
      class="hero-radar pointer-events-none absolute -end-28 top-1/2 hidden aspect-square w-[480px] -translate-y-1/2 md:block"
      aria-hidden="true"
    >
      <span class="hero-ring absolute inset-0" />
      <span class="hero-ring absolute inset-[16%]" />
      <span class="hero-ring absolute inset-[32%]" />
      <span class="hero-ring absolute inset-[46%]" />
      <span class="hero-cross absolute inset-0" />
      <span class="hero-sweep absolute inset-0" />
      <span
        v-for="(blip, i) in BLIPS"
        :key="i"
        class="hero-blip absolute h-2 w-2 -translate-x-1/2 -translate-y-1/2 rounded-full"
        :style="{ top: blip.top, left: blip.left, animationDelay: blip.delay }"
      />
    </div>

    <div
      class="absolute inset-0 bg-gradient-to-r from-[#0a0c0f]/95 via-[#0a0c0f]/55 to-transparent rtl:bg-gradient-to-l"
      aria-hidden="true"
    />
    <div
      class="absolute inset-x-0 bottom-0 h-2/3 bg-gradient-to-t from-[#0a0c0f] via-[#0a0c0f]/50 to-transparent"
      aria-hidden="true"
    />
    <div class="hero-edge absolute inset-x-0 bottom-0 h-px" aria-hidden="true" />

    <div class="relative flex flex-1 flex-col">
      <slot />
    </div>
  </section>
</template>

<style scoped>
@property --hero-accent {
  syntax: "<color>";
  inherits: true;
  initial-value: #e3d39a;
}

.servers-hero {
  --hero-x: 82%;
  transition: --hero-accent 1.6s ease;
}

:global([dir="rtl"]) .servers-hero {
  --hero-x: 18%;
}

.hero-slide {
  animation: hero-drift 26s ease-in-out infinite alternate;
}

.hero-wash {
  background:
    radial-gradient(
      ellipse 45% 70% at var(--hero-x) 50%,
      color-mix(in srgb, var(--hero-accent) 32%, transparent),
      transparent 70%
    ),
    radial-gradient(
      ellipse 40% 50% at calc(100% - var(--hero-x)) 100%,
      color-mix(in srgb, var(--hero-accent) 10%, transparent),
      transparent 70%
    );
}

.hero-grid {
  background-image:
    linear-gradient(rgb(255 255 255 / 0.045) 1px, transparent 1px),
    linear-gradient(90deg, rgb(255 255 255 / 0.045) 1px, transparent 1px);
  background-size: 36px 36px;
  mask-image: radial-gradient(
    ellipse 60% 90% at var(--hero-x) 50%,
    black,
    transparent 75%
  );
}

.hero-ring {
  border-radius: 9999px;
  border: 1px solid color-mix(in srgb, var(--hero-accent) 45%, transparent);
  box-shadow: inset 0 0 24px color-mix(in srgb, var(--hero-accent) 8%, transparent);
}

.hero-cross {
  border-radius: 9999px;
  background:
    linear-gradient(
      color-mix(in srgb, var(--hero-accent) 18%, transparent),
      color-mix(in srgb, var(--hero-accent) 18%, transparent)
    )
      center / 1px 100% no-repeat,
    linear-gradient(
      color-mix(in srgb, var(--hero-accent) 18%, transparent),
      color-mix(in srgb, var(--hero-accent) 18%, transparent)
    )
      center / 100% 1px no-repeat;
}

.hero-sweep {
  border-radius: 9999px;
  background: conic-gradient(
    from 0deg,
    color-mix(in srgb, var(--hero-accent) 55%, transparent),
    transparent 24%
  );
  animation: hero-spin 7s linear infinite;
}

.hero-blip {
  background: var(--hero-accent);
  box-shadow: 0 0 12px 2px var(--hero-accent);
  animation: hero-blip 3.6s ease-out infinite;
}

.hero-edge {
  background: linear-gradient(
    90deg,
    transparent,
    color-mix(in srgb, var(--hero-accent) 70%, transparent),
    transparent
  );
}

@keyframes hero-drift {
  from {
    transform: scale(1.04) translate3d(0, 0, 0);
  }
  to {
    transform: scale(1.14) translate3d(-1.5%, -1%, 0);
  }
}

@keyframes hero-spin {
  to {
    transform: rotate(360deg);
  }
}

@keyframes hero-blip {
  0%,
  100% {
    opacity: 0;
    transform: translate(-50%, -50%) scale(0.6);
  }
  12% {
    opacity: 1;
    transform: translate(-50%, -50%) scale(1);
  }
  60% {
    opacity: 0.15;
  }
}

@media (prefers-reduced-motion: reduce) {
  .hero-slide,
  .hero-sweep,
  .hero-blip {
    animation: none;
  }
  .hero-blip {
    opacity: 0.6;
  }
}
</style>
