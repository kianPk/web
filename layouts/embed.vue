<script setup lang="ts">
import { defineAsyncComponent } from "vue";

const ActionToasts = defineAsyncComponent(
  () => import("~/components/notification/ActionToasts.vue"),
);
</script>

<template>
  <div
    class="embed-shell box-border flex h-full min-h-0 w-full flex-col overflow-x-hidden overflow-y-auto bg-[#0c0d10] text-[#e8eaed]"
  >
    <div
      class="embed-pad mx-auto flex w-full max-w-[1400px] flex-1 flex-col gap-8 px-4 py-5 sm:gap-10 sm:px-6 sm:py-7 lg:gap-12 lg:px-10 lg:py-9"
    >
      <slot />
    </div>
    <ClientOnly>
      <ActionToasts />
    </ClientOnly>
  </div>
</template>
