<script setup lang="ts">
import { defineAsyncComponent } from "vue";

const ActionToasts = defineAsyncComponent(
  () => import("~/components/notification/ActionToasts.vue"),
);
</script>

<template>
  <div
    class="box-border min-h-0 h-full w-full overflow-x-hidden overflow-y-auto bg-[#0c0d10] text-[#e8eaed]"
  >
    <div class="mx-auto w-full max-w-[1600px] px-3 py-3 sm:px-4 sm:py-4 lg:px-6">
      <slot />
    </div>
    <ClientOnly>
      <ActionToasts />
    </ClientOnly>
  </div>
</template>
