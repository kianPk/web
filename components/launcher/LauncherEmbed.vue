<script setup lang="ts">
import { computed, ref } from "vue";

const props = defineProps<{
  src: string;
}>();

const loaded = ref(false);
const embedSrc = computed(() => {
  const url = new URL(props.src, "https://local.invalid");
  url.searchParams.set("embed", "1");
  return `${url.pathname}${url.search}${url.hash}`;
});
</script>

<template>
  <div class="relative h-full w-full bg-[#0c0d10]">
    <div
      v-if="!loaded"
      class="pointer-events-none absolute inset-0 animate-pulse bg-[linear-gradient(180deg,#15171e_0%,#0c0d10_100%)]"
    />
    <iframe
      :src="embedSrc"
      class="h-full w-full border-0 bg-[#0c0d10]"
      title="YGuard"
      @load="loaded = true"
    />
  </div>
</template>
