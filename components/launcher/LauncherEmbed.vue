<script setup lang="ts">
import { computed, ref, watch } from "vue";

const props = defineProps<{
  src: string;
}>();

const loaded = ref(false);
const embedSrc = computed(() => {
  const url = new URL(props.src, "https://local.invalid");
  url.searchParams.set("embed", "1");
  return `${url.pathname}${url.search}${url.hash}`;
});

watch(
  () => props.src,
  () => {
    loaded.value = false;
  },
);
</script>

<template>
  <div class="absolute inset-0 overflow-hidden rounded-[inherit] bg-[#0c0d10]">
    <div
      v-if="!loaded"
      class="pointer-events-none absolute inset-0 z-10 animate-pulse bg-[linear-gradient(180deg,rgba(21,23,30,0.9)_0%,rgba(12,13,16,0.95)_100%)]"
    />
    <iframe
      :key="embedSrc"
      :src="embedSrc"
      class="absolute inset-0 h-full w-full border-0 bg-[#0c0d10]"
      title="YGuard"
      allow="clipboard-read; clipboard-write"
      @load="loaded = true"
    />
  </div>
</template>
