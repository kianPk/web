<script setup lang="ts">
import { defineAsyncComponent } from "vue";

const ActionToasts = defineAsyncComponent(
  () => import("~/components/notification/ActionToasts.vue"),
);
</script>

<template>
  <div class="min-h-screen bg-[#0c0d10] text-[#e8eaed]">
    <slot />
    <ClientOnly>
      <ActionToasts />
    </ClientOnly>
  </div>
</template>
