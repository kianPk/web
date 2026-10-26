<script setup lang="ts">
import { computed, defineAsyncComponent } from "vue";

const ActionToasts = defineAsyncComponent(
  () => import("~/components/notification/ActionToasts.vue"),
);

const route = useRoute();

/** Inventory / full-bleed plugin surfaces — no padded “page card”. */
const fullBleed = computed(() => {
  const path = route.path.replace(/\/$/, "");
  return (
    path.startsWith("/apps/") ||
    path.startsWith("/battle-pass") ||
    path.startsWith("/scrims")
  );
});
</script>

<template>
  <div
    class="embed-shell box-border flex h-full min-h-0 w-full flex-col overflow-x-hidden overflow-y-auto text-[#e8eaed]"
    :class="fullBleed ? 'embed-shell--bleed' : 'embed-shell--padded'"
  >
    <div
      class="embed-pad mx-auto flex w-full flex-1 flex-col"
      :class="
        fullBleed
          ? 'max-w-none gap-0 p-0'
          : 'max-w-[1400px] gap-8 px-4 py-5 sm:gap-10 sm:px-6 sm:py-7 lg:gap-12 lg:px-10 lg:py-9'
      "
    >
      <slot />
    </div>
    <ClientOnly>
      <ActionToasts />
    </ClientOnly>
  </div>
</template>

<style scoped>
.embed-shell--padded {
  background: transparent;
}

.embed-shell--bleed {
  background: transparent;
}
</style>
